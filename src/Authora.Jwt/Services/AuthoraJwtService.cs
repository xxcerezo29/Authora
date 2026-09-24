using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Authora.Core.Abstractions;
using Authora.Jwt.Options;
using Microsoft.IdentityModel.Tokens;
using Authora.Jwt.Authentication;

namespace Authora.Jwt.Services;

/// <summary>
/// Represents a signed JWT access token and its expiration.
/// </summary>
/// <param name="AccessToken">Compact signed JWT value.</param>
/// <param name="TokenType">Bearer token type.</param>
/// <param name="ExpiresAt">UTC access token expiration time.</param>
public sealed record CreatedJwt(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

/// <summary>
/// Creates signed JWT access tokens after resolving the application subject.
/// </summary>
public sealed class AuthoraJwtService
{
  private readonly AuthoraJwtOptions _options;
  private readonly IAuthoraSubjectResolver _subjects;
  private readonly TimeProvider _clock;

  /// <summary>
  /// Initializes a new instance of AuthoraJwtService.
  /// </summary>
  public AuthoraJwtService(
    AuthoraJwtOptions options,
    IAuthoraSubjectResolver subjects,
    TimeProvider clock
  )
  {
    _options = options;
    _subjects = subjects;
    _clock = clock;
  }

  /// <summary>
  /// Creates a signed JWT for an available application subject.
  /// </summary>
  /// <param name="subjectId">Identifier of the application user.</param>
  /// <param name="cancellationToken">Token used to cancel subject resolution.</param>
  /// <param name="sessionId">Optional revocable session identifier included in the token.</param>
  /// <returns>The bearer token and its expiration.</returns>
  public async Task<CreatedJwt> CreateAsync(
    string subjectId,
    CancellationToken cancellationToken = default,
    Guid? sessionId = null
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

    var subject = await _subjects.ResolveAsync(subjectId, cancellationToken);

    if (subject is null || !string.Equals(subject.Id, subjectId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("The subject is not available.");
    }

    var now = _clock.GetUtcNow();

    var expiresAt = now.Add(_options.AccessTokenLifetime);

    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, subject.Id),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
    };

    if (sessionId.HasValue)
    {
      claims.Add(new Claim(AuthoraJwtDefaults.SessionIdClaim, sessionId.Value.ToString("N")));
    }

    var key = new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKeyBase64));

    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
      issuer: _options.Issuer,
      audience: _options.Audience,
      claims: claims,
      notBefore: now.UtcDateTime,
      expires: expiresAt.UtcDateTime,
      signingCredentials: credentials
    );

    var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

    return new CreatedJwt(AccessToken: accessToken, TokenType: "Bearer", ExpiresAt: expiresAt);
  }
}
