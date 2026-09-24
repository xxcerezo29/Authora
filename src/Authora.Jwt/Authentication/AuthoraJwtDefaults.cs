namespace Authora.Jwt.Authentication;

/// <summary>
/// Defines the JWT authentication scheme and session claim name.
/// </summary>
public static class AuthoraJwtDefaults
{
  /// <summary>
  /// Authentication scheme name used by Authora JWT bearer tokens.
  /// </summary>
  public const string Scheme = "Authora.Jwt";

  /// <summary>
  /// Claim containing the revocable session identifier in a JWT.
  /// </summary>
  public const string SessionIdClaim = "authora:session_id";
}
