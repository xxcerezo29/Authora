namespace Authora.Jwt.Services;

/// <summary>
/// Represents the access and refresh credentials issued for one JWT session.
/// </summary>
/// <param name="AccessToken">Short-lived signed JWT bearer credential.</param>
/// <param name="RefreshToken">Opaque secret used once to obtain a replacement token pair.</param>
/// <param name="TokenType">Token type returned to API clients, normally `Bearer`.</param>
/// <param name="SessionId">Revocable server-side session identifier.</param>
/// <param name="AccessTokenExpiresAt">UTC access token expiration time.</param>
/// <param name="RefreshTokenExpiresAt">UTC refresh token expiration time.</param>
public sealed record AuthoraTokenPair(
  string AccessToken,
  string RefreshToken,
  string TokenType,
  Guid SessionId,
  DateTimeOffset AccessTokenExpiresAt,
  DateTimeOffset RefreshTokenExpiresAt
);
