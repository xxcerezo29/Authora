namespace Authora.AspNetCore.Session;

/// <summary>
/// Defines the browser session authentication scheme and default cookie name.
/// </summary>
public static class AuthoraSessionDefaults
{
  /// <summary>
  /// Authentication scheme name used by browser sessions.
  /// </summary>
  public const string Scheme = "Authora.Session";

  /// <summary>
  /// Default Secure host-only cookie name for browser sessions.
  /// </summary>
  public const string CookieName = "__Host-Authora";
}
