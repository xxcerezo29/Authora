namespace Authora.Core.Options;

/// <summary>
/// Configures personal access token defaults.
/// </summary>
public sealed class AuthoraOptions
{
  /// <summary>
  /// Gets or sets the lifetime assigned to newly created tokens. Must be positive.
  /// </summary>
  public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(30);
}
