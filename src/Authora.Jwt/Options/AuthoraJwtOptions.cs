namespace Authora.Jwt.Options;

/// <summary>
/// Configures JWT issuer, audience, signing key, and access token lifetime.
/// </summary>
public sealed class AuthoraJwtOptions
{
  /// <summary>
  /// Gets or sets the required JWT issuer value.
  /// </summary>
  public string Issuer { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the required JWT audience value.
  /// </summary>
  public string Audience { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a Base64-encoded symmetric key containing at least 32 bytes.
  /// </summary>
  public string SigningKeyBase64 { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the maximum lifetime of a newly issued access token. Must be positive.
  /// </summary>
  public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
