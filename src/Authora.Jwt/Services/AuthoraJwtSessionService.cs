using System.Security.Cryptography;
using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.Core.Options;

namespace Authora.Jwt.Services;

/// <summary>
/// Manages JWT sessions and rotates their opaque refresh tokens.
/// </summary>
public sealed class AuthoraJwtSessionService
{
  private const string RefreshPrefix = "arr_";

  private readonly IAuthoraRefreshStore _store;
  private readonly AuthoraJwtService _jwt;
  private readonly AuthoraJwtSessionOptions _options;
  private readonly TimeProvider _clock;

  /// <summary>
  /// Initializes a new instance of AuthoraJwtSessionService.
  /// </summary>
  public AuthoraJwtSessionService(
    IAuthoraRefreshStore store,
    AuthoraJwtService jwt,
    AuthoraJwtSessionOptions options,
    TimeProvider clock
  )
  {
    _store = store;
    _jwt = jwt;
    _options = options;
    _clock = clock;
  }

  /// <summary>
  /// Starts a revocable session and issues an access/refresh token pair.
  /// </summary>
  /// <param name="subjectId">Identifier of the application user.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns>The access and refresh token pair with expiration values.</returns>
  public async Task<AuthoraTokenPair> SignInAsync(
    string subjectId,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

    var now = _clock.GetUtcNow();

    var session = AuthoraJwtSession.Create(
      id: Guid.NewGuid(),
      subjectId: subjectId,
      createdAt: now,
      expiresAt: now.Add(_options.SessionLifetime)
    );

    var (token, refreshSecret) = CreateRefreshToken(session, now);

    // The JWT service checks whether the subject is available.
    // Do not persist a login session if JWT issuance fails.
    var accessToken = await _jwt.CreateAsync(subjectId, cancellationToken, session.Id);

    await _store.CreateAsync(session, token, cancellationToken);

    return new AuthoraTokenPair(
      AccessToken: accessToken.AccessToken,
      RefreshToken: FormatRefreshToken(token.Id, refreshSecret),
      TokenType: "Bearer",
      SessionId: session.Id,
      AccessTokenExpiresAt: accessToken.ExpiresAt,
      RefreshTokenExpiresAt: token.ExpiresAt
    );
  }

  /// <summary>
  /// Validates and rotates a refresh token, returning <see langword="null"/> when invalid.
  /// </summary>
  /// <param name="plainTextRefreshToken">The complete refresh token supplied by the client.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns>A new token pair, or <see langword="null"/> if the token cannot be rotated.</returns>
  public async Task<AuthoraTokenPair?> RefreshAsync(
    string plainTextRefreshToken,
    CancellationToken cancellationToken = default
  )
  {
    if (!TryParseRefreshToken(plainTextRefreshToken, out var tokenId, out var suppliedSecret))
    {
      return null;
    }

    var current = await _store.FindTokenAsync(tokenId, cancellationToken);

    if (current is null || !SecretMatches(suppliedSecret, current.SecretHash))
    {
      return null;
    }

    var now = _clock.GetUtcNow();

    var session = await _store.FindSessionAsync(current.SessionId, cancellationToken);

    if (session is null || !session.IsActiveAt(now))
      return null;

    // A correctly presented token that was already consumed
    // indicates a replay or a concurrent refresh attempt.
    if (current.ConsumedAt is not null)
    {
      await _store.RevokeSessionAsync(session.Id, session.SubjectId, now, cancellationToken);

      return null;
    }

    if (current.ExpiresAt <= now)
      return null;

    var (replacement, replacementSecret) = CreateRefreshToken(session, now);

    // Generate the next access token before consuming
    // the existing refresh token.
    var accessToken = await _jwt.CreateAsync(session.SubjectId, cancellationToken, session.Id);

    var rotated = await _store.TryRotateAsync(current.Id, replacement, now, cancellationToken);

    if (!rotated)
    {
      // Re-read the record to distinguish a consumed
      // token from an ordinary failed rotation.
      var latest = await _store.FindTokenAsync(current.Id, cancellationToken);

      if (latest?.ConsumedAt is not null)
      {
        await _store.RevokeSessionAsync(
          session.Id,
          session.SubjectId,
          _clock.GetUtcNow(),
          cancellationToken
        );
      }

      return null;
    }

    return new AuthoraTokenPair(
      AccessToken: accessToken.AccessToken,
      RefreshToken: FormatRefreshToken(replacement.Id, replacementSecret),
      TokenType: "Bearer",
      SessionId: session.Id,
      AccessTokenExpiresAt: accessToken.ExpiresAt,
      RefreshTokenExpiresAt: replacement.ExpiresAt
    );
  }

  /// <summary>
  /// Revokes the session represented by a valid refresh token.
  /// </summary>
  /// <param name="plainTextRefreshToken">The refresh token supplied by the client.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns><see langword="true"/> when the session was revoked.</returns>
  public async Task<bool> RevokeAsync(
    string plainTextRefreshToken,
    CancellationToken cancellationToken = default
  )
  {
    if (!TryParseRefreshToken(plainTextRefreshToken, out var tokenId, out var suppliedSecret))
    {
      return false;
    }

    var token = await _store.FindTokenAsync(tokenId, cancellationToken);

    if (token is null || !SecretMatches(suppliedSecret, token.SecretHash))
    {
      return false;
    }

    var session = await _store.FindSessionAsync(token.SessionId, cancellationToken);

    if (session is null)
      return false;

    return await RevokeSessionAsync(session.Id, session.SubjectId, cancellationToken);
  }

  /// <summary>
  /// Revokes a session when it belongs to the specified subject.
  /// </summary>
  /// <param name="sessionId">Identifier of the session to revoke.</param>
  /// <param name="subjectId">Owner identifier used to scope the revoke operation.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns><see langword="true"/> when an active session was revoked.</returns>
  public Task<bool> RevokeSessionAsync(
    Guid sessionId,
    string subjectId,
    CancellationToken cancellationToken = default
  )
  {
    return _store.RevokeSessionAsync(sessionId, subjectId, _clock.GetUtcNow(), cancellationToken);
  }

  private (AuthoraRefreshToken Token, byte[] Secret) CreateRefreshToken(
    AuthoraJwtSession session,
    DateTimeOffset now
  )
  {
    var secret = RandomNumberGenerator.GetBytes(32);

    var hash = Convert.ToHexString(SHA256.HashData(secret));

    var requestedExpiration = now.Add(_options.RefreshTokenLifetime);

    var expiresAt =
      requestedExpiration < session.ExpiresAt ? requestedExpiration : session.ExpiresAt;

    var token = AuthoraRefreshToken.Create(
      id: Guid.NewGuid(),
      sessionId: session.Id,
      secretHash: hash,
      createdAt: now,
      expiresAt: expiresAt
    );

    return (token, secret);
  }

  private static string FormatRefreshToken(Guid id, byte[] secret)
  {
    return $"{RefreshPrefix}{id:N}_{Convert.ToHexString(secret)}";
  }

  private static bool TryParseRefreshToken(string? value, out Guid tokenId, out byte[] secret)
  {
    tokenId = Guid.Empty;
    secret = [];

    // "arr_" + 32-character GUID + "_" + 64 hex characters
    if (
      value is null
      || value.Length != 101
      || !value.StartsWith(RefreshPrefix, StringComparison.Ordinal)
      || value[36] != '_'
    )
    {
      return false;
    }

    if (!Guid.TryParseExact(value.AsSpan(4, 32), "N", out tokenId))
    {
      return false;
    }

    try
    {
      secret = Convert.FromHexString(value[37..]);
      return secret.Length == 32;
    }
    catch (FormatException)
    {
      return false;
    }
  }

  private static bool SecretMatches(byte[] suppliedSecret, string storedHash)
  {
    var suppliedHash = SHA256.HashData(suppliedSecret);

    byte[] expectedHash;

    try
    {
      expectedHash = Convert.FromHexString(storedHash);
    }
    catch (FormatException)
    {
      return false;
    }

    return expectedHash.Length == suppliedHash.Length
      && CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
  }
}
