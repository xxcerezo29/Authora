using Authora.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace Authora.AspNetCore.Authorization;

/// <summary>
/// Adds authorization policies for Authora personal access token abilities.
/// </summary>
public static class AuthoraAuthorizationExtensions
{
  /// <summary>
  /// Requires an authenticated personal access token with the specified ability.
  /// </summary>
  public static TBuilder RequireAuthoraAbility<TBuilder>(this TBuilder builder, string ability)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(ability);

    var policy = new AuthorizationPolicyBuilder(AuthoraDefaults.TokenScheme)
      .RequireAuthenticatedUser()
      .RequireClaim(AuthoraDefaults.AbilityClaim, ability, "*")
      .Build();

    return builder.RequireAuthorization(policy);
  }
}
