using Authora.Core.Options;
using Authora.Jwt.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Jwt.Extensions;

/// <summary>
/// Registers the JWT session service and its lifetime settings.
/// </summary>
public static class AuthoraJwtSessionExtensions
{
  /// <summary>
  /// Registers JWT session services with dependency injection.
  /// </summary>
  /// <param name="services">The application's service collection.</param>
  /// <param name="configure">Optional session and refresh lifetime configuration.</param>
  /// <returns>The same service collection for additional registrations.</returns>
  public static IServiceCollection AddAuthoraJwtSessions(
    this IServiceCollection services,
    Action<AuthoraJwtSessionOptions>? configure = null
  )
  {
    var options = new AuthoraJwtSessionOptions();

    configure?.Invoke(options);

    if (options.RefreshTokenLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(configure),
        "Refresh token lifetime must be positive."
      );
    }

    if (options.SessionLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(configure),
        "Session lifetime must be positive."
      );
    }

    services.AddSingleton(options);

    services.AddScoped<AuthoraJwtSessionService>();

    return services;
  }
}
