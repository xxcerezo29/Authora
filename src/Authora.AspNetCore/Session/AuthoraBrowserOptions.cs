namespace Authora.AspNetCore.Session;

/// <summary>
/// Configures the server-side browser session cookie and lifetime.
/// </summary>
public sealed class AuthoraBrowserOptions
{
  /// <summary>
  /// Gets or sets the fixed lifetime of a browser session. Must be positive.
  /// </summary>
  public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

  /// <summary>
  /// Gets or sets the browser session cookie name; the default uses the `__Host-` prefix.
  /// </summary>
  public string CookieName { get; set; } = AuthoraSessionDefaults.CookieName;
}
