namespace Authora.Core.Options;

/// <summary>
/// Configures JWT session and refresh token lifetimes.
/// </summary>
public sealed class AuthoraJwtSessionOptions
{
  /// <summary>
  /// Gets or sets the maximum lifetime of a refresh token, capped by its session expiry.
  /// </summary>
  public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

  /// <summary>
  /// Gets or sets the maximum lifetime of a revocable JWT session.
  /// </summary>
  public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);
}
