using System.Security.Claims;
using Authora.AspNetCore.Session;
using Authora.Core.Abstractions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Authora.AspNetCore.Extensions;

/// <summary>
/// Registers Authora browser sessions and adds session/CSRF endpoint conventions.
/// </summary>
public static class AuthoraBrowserExtensions
{
  /// <summary>
  /// Registers cookie authentication, server-side session tickets, and antiforgery services.
  /// </summary>
  /// <param name="services">The application's service collection.</param>
  /// <param name="configure">Optional session lifetime and cookie name configuration.</param>
  /// <returns>The same service collection for additional registrations.</returns>
  public static IServiceCollection AddAuthoraBrowserSessions(
    this IServiceCollection services,
    Action<AuthoraBrowserOptions>? configure = null
  )
  {
    var settings = new AuthoraBrowserOptions();

    configure?.Invoke(settings);

    if (settings.SessionLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(configure),
        "Session lifetime must be positive."
      );
    }

    services.AddSingleton(settings);

    services.TryAddSingleton<TimeProvider>(TimeProvider.System);

    services.AddSingleton<AuthoraTicketStore>();

    services.AddScoped<AuthoraBrowserSessionService>();

    services
      .AddAuthentication()
      .AddCookie(
        AuthoraSessionDefaults.Scheme,
        options =>
        {
          options.Cookie.Name = settings.CookieName;

          options.Cookie.HttpOnly = true;

          options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

          options.Cookie.SameSite = SameSiteMode.Lax;

          options.Cookie.Path = "/";

          options.ExpireTimeSpan = settings.SessionLifetime;

          options.SlidingExpiration = false;

          options.LoginPath = "/auth/session/login";

          options.Events.OnRedirectToLogin = context =>
          {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
          };

          options.Events.OnRedirectToAccessDenied = context =>
          {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
          };

          options.Events.OnValidatePrincipal = async context =>
          {
            var subjectId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(subjectId))
            {
              context.RejectPrincipal();
              return;
            }

            var resolver =
              context.HttpContext.RequestServices.GetRequiredService<IAuthoraSubjectResolver>();

            var subject = await resolver.ResolveAsync(
              subjectId,
              context.HttpContext.RequestAborted
            );

            if (subject is null || !string.Equals(subject.Id, subjectId, StringComparison.Ordinal))
            {
              context.RejectPrincipal();
              return;
            }

            var claims = subject
              .Claims.Where(claim =>
                claim.Type != ClaimTypes.NameIdentifier
                && !claim.Type.StartsWith("authora:", StringComparison.Ordinal)
              )
              .ToList();

            claims.Add(new Claim(ClaimTypes.NameIdentifier, subject.Id));

            var identity = new ClaimsIdentity(
              claims,
              AuthoraSessionDefaults.Scheme,
              ClaimTypes.Name,
              ClaimTypes.Role
            );

            context.ReplacePrincipal(new ClaimsPrincipal(identity));

            context.ShouldRenew = false;
          };
        }
      );

    services
      .AddOptions<CookieAuthenticationOptions>(AuthoraSessionDefaults.Scheme)
      .Configure<AuthoraTicketStore>(
        (options, store) =>
        {
          options.SessionStore = store;
        }
      );

    services.AddAntiforgery(options =>
    {
      options.HeaderName = "X-Authora-CSRF";

      options.Cookie.Name = "__Host-Authora.Csrf";

      options.Cookie.HttpOnly = true;

      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

      options.Cookie.SameSite = SameSiteMode.Lax;

      options.Cookie.Path = "/";
    });

    services.AddAuthorization();

    return services;
  }

  /// <summary>
  /// Requires an authenticated Authora browser session for the endpoint.
  /// </summary>
  /// <param name="builder">The endpoint convention builder.</param>
  /// <returns>The builder for further endpoint configuration.</returns>
  public static TBuilder RequireAuthoraSession<TBuilder>(this TBuilder builder)
    where TBuilder : IEndpointConventionBuilder
  {
    var policy = new AuthorizationPolicyBuilder(AuthoraSessionDefaults.Scheme)
      .RequireAuthenticatedUser()
      .Build();

    return builder.RequireAuthorization(policy);
  }

  /// <summary>
  /// Requires a valid antiforgery request token for the endpoint.
  /// </summary>
  /// <param name="builder">The route handler builder.</param>
  /// <returns>The builder for further endpoint configuration.</returns>
  public static RouteHandlerBuilder RequireAuthoraCsrf(this RouteHandlerBuilder builder)
  {
    return builder.AddEndpointFilter(
      async (context, next) =>
      {
        var http = context.HttpContext;

        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();

        if (!await antiforgery.IsRequestValidAsync(http))
        {
          return Results.BadRequest("Invalid CSRF token.");
        }

        return await next(context);
      }
    );
  }

  /// <summary>
  /// Adds automatic CSRF checks for unsafe requests carrying an Authora session cookie.
  /// </summary>
  /// <param name="app">The application pipeline; call after authentication middleware.</param>
  /// <returns>The application builder for further pipeline configuration.</returns>
  public static IApplicationBuilder UseAuthoraSessionCsrf(this IApplicationBuilder app)
  {
    return app.Use(
      async (context, next) =>
      {
        var method = context.Request.Method;

        var isSafeMethod =
          HttpMethods.IsGet(method)
          || HttpMethods.IsHead(method)
          || HttpMethods.IsOptions(method)
          || HttpMethods.IsTrace(method);

        if (isSafeMethod)
        {
          await next(context);
          return;
        }

        var settings = context.RequestServices.GetRequiredService<AuthoraBrowserOptions>();

        // Only requests carrying a browser auth cookie
        // require automatic browser-session CSRF validation.
        if (!context.Request.Cookies.ContainsKey(settings.CookieName))
        {
          await next(context);
          return;
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();

        if (!await antiforgery.IsRequestValidAsync(context))
        {
          context.Response.StatusCode = StatusCodes.Status400BadRequest;

          return;
        }

        await next(context);
      }
    );
  }
}
