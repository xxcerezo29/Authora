namespace Authora.AspNetCore.Authentication;

/// <summary>
/// Defines the personal access token authentication scheme and claim names.
/// </summary>
public static class AuthoraDefaults
{
  /// <summary>
  /// Authentication scheme name used by personal access tokens.
  /// </summary>
  public const string TokenScheme = "Authora.Token";

  /// <summary>
  /// Claim containing the personal access token identifier.
  /// </summary>
  public const string TokenIdClaim = "authora:token_id";

  /// <summary>
  /// Claim containing an ability granted to the current token.
  /// </summary>
  public const string AbilityClaim = "authora:ability";
}
