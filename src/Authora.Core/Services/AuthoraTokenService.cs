using System.Security.Cryptography;
using System.Text.Json;
using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.Core.Options;
using Authora.Core.Results;

namespace Authora.Core.Services;

/// <summary>
/// Creates, authenticates, and revokes hashed personal access tokens.
/// </summary>
public sealed class AuthoraTokenService
{
  private readonly IAuthoraTokenStore _store;
  private readonly TimeProvider _clock;
  private readonly AuthoraOptions _options;

  /// <summary>
  /// Initializes the token service with persistence, time, and lifetime settings.
  /// </summary>
  /// <param name="store">Persistence for token records.</param>
  /// <param name="clock">Clock used for token creation and expiry checks.</param>
  /// <param name="options">Token lifetime configuration.</param>
  public AuthoraTokenService(IAuthoraTokenStore store, TimeProvider clock, AuthoraOptions options)
  {
    _store = store;
    _clock = clock;
    _options = options;
  }

  /// <summary>
  /// Creates a personal access token and returns its plaintext secret once.
  /// </summary>
  /// <param name="subjectId">Identifier of the application user who owns the token.</param>
  /// <param name="name">User-visible name for the client or device.</param>
  /// <param name="abilities">Optional ability strings granted to the token.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns>The token ID, one-time plaintext token, and expiration time.</returns>
  /// <exception cref="ArgumentException">The subject, name, or ability list is invalid.</exception>
  public async Task<CreatedAccessToken> CreateAsync(
    string subjectId,
    string name,
    IEnumerable<string>? abilities = null,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    if (subjectId.Length > 128 || name.Length > 128)
      throw new ArgumentException("Subject ID and token name must not exceed 128 characters.");

    var abilityList = (abilities ?? []).Distinct(StringComparer.Ordinal).ToArray();

    if (
      abilityList.Length > 64
      || abilityList.Any(a => string.IsNullOrWhiteSpace(a) || a.Length > 128)
    )
    {
      throw new ArgumentException("Invalid token abilities.", nameof(abilities));
    }

    var now = _clock.GetUtcNow();

    var id = Guid.NewGuid();

    // Generate a cryptographically secure 256-bit secret.
    var secretBytes = RandomNumberGenerator.GetBytes(32);

    var secret = Convert.ToHexString(secretBytes);

    var hash = Convert.ToHexString(SHA256.HashData(secretBytes));

    var expiresAt = now.Add(_options.TokenLifetime);

    var token = PersonalAccessToken.Create(
      id,
      subjectId,
      name,
      hash,
      JsonSerializer.Serialize(abilityList),
      now,
      expiresAt
    );

    await _store.CreateAsync(token, cancellationToken);

    // Format: ara_{token-id}_{secret}
    var plainTextToken = $"ara_{id:N}_{secret}";

    return new CreatedAccessToken(id, plainTextToken, expiresAt);
  }

  /// <summary>
  /// Authenticates a plaintext token and returns its stored record when valid.
  /// </summary>
  /// <param name="plainTextToken">The complete token supplied by the client.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns>The token record, or <see langword="null"/> for malformed, unknown, revoked, or expired tokens.</returns>
  public async Task<PersonalAccessToken?> AuthenticateAsync(
    string plainTextToken,
    CancellationToken cancellationToken = default
  )
  {
    // ara_ + 32 GUID characters + _ + 64 secret characters
    if (
      plainTextToken is null
      || plainTextToken.Length != 101
      || !plainTextToken.StartsWith("ara_", StringComparison.Ordinal)
      || plainTextToken[36] != '_'
    )
    {
      return null;
    }

    if (!Guid.TryParseExact(plainTextToken.AsSpan(4, 32), "N", out var tokenId))
    {
      return null;
    }

    byte[] suppliedSecret;

    try
    {
      suppliedSecret = Convert.FromHexString(plainTextToken[37..]);
    }
    catch (FormatException)
    {
      return null;
    }

    var token = await _store.FindByIdAsync(tokenId, cancellationToken);

    if (token is null || !token.IsValidAt(_clock.GetUtcNow()))
    {
      return null;
    }

    var suppliedHash = SHA256.HashData(suppliedSecret);

    var storedHash = Convert.FromHexString(token.SecretHash);

    if (!CryptographicOperations.FixedTimeEquals(suppliedHash, storedHash))
    {
      return null;
    }

    return token;
  }

  /// <summary>
  /// Revokes a token when it belongs to the specified subject.
  /// </summary>
  /// <param name="tokenId">Identifier of the token to revoke.</param>
  /// <param name="subjectId">Owner identifier used to scope the revoke operation.</param>
  /// <param name="cancellationToken">Token used to cancel persistence work.</param>
  /// <returns><see langword="true"/> when a token was revoked.</returns>
  public Task<bool> RevokeAsync(
    Guid tokenId,
    string subjectId,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

    return _store.RevokeAsync(tokenId, subjectId, _clock.GetUtcNow(), cancellationToken);
  }
}
