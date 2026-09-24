using Authora.AspNetCore.Authentication;
using Authora.Core.Options;
using Authora.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Authora.AspNetCore.Extensions;

/// <summary>
/// Registers Authora personal access token services and bearer authentication.
/// </summary>
public static class AuthoraServiceExtensions
{
  /// <summary>
  /// Registers token authentication, authorization, and token service dependencies.
  /// </summary>
  /// <param name="services">The application's service collection.</param>
  /// <param name="configure">Optional token lifetime configuration.</param>
  /// <returns>The same service collection for additional registrations.</returns>
  public static IServiceCollection AddAuthoraTokens(
    this IServiceCollection services,
    Action<AuthoraOptions>? configure = null
  )
  {
    var options = new AuthoraOptions();

    configure?.Invoke(options);

    if (options.TokenLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(configure),
        "Token lifetime must be greater than zero."
      );
    }

    services.AddSingleton(options);

    services.TryAddSingleton<TimeProvider>(TimeProvider.System);

    services.AddScoped<AuthoraTokenService>();

    services
      .AddAuthentication(AuthoraDefaults.TokenScheme)
      .AddScheme<AuthenticationSchemeOptions, AuthoraTokenHandler>(
        AuthoraDefaults.TokenScheme,
        _ => { }
      );

    services.AddAuthorization();

    return services;
  }
}
