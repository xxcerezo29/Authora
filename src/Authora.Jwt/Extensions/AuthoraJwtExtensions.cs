using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Authora.Core.Abstractions;
using Authora.Jwt.Authentication;
using Authora.Jwt.Options;
using Authora.Jwt.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Authora.Jwt.Extensions;

/// <summary>
/// Registers JWT issuance and bearer-token validation services.
/// </summary>
public static class AuthoraJwtExtensions
{
  /// <summary>
  /// Registers JWT bearer validation and token creation services.
  /// </summary>
  /// <param name="services">The application's service collection.</param>
  /// <param name="configure">Required issuer, audience, signing key, and token lifetime settings.</param>
  /// <returns>The same service collection for additional registrations.</returns>
  public static IServiceCollection AddAuthoraJwt(
    this IServiceCollection services,
    Action<AuthoraJwtOptions> configure
  )
  {
    var settings = new AuthoraJwtOptions();

    configure(settings);

    ArgumentException.ThrowIfNullOrWhiteSpace(settings.Issuer);

    ArgumentException.ThrowIfNullOrWhiteSpace(settings.Audience);

    var keyBytes = Convert.FromBase64String(settings.SigningKeyBase64);

    if (keyBytes.Length < 32)
    {
      throw new ArgumentException("JWT signing key must contain at least 32 bytes.");
    }

    if (settings.AccessTokenLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentException("JWT access token lifetime must be positive.");
    }

    services.AddSingleton(settings);

    services.TryAddSingleton<TimeProvider>(TimeProvider.System);

    services.AddScoped<AuthoraJwtService>();

    services
      .AddAuthentication()
      .AddJwtBearer(
        AuthoraJwtDefaults.Scheme,
        options =>
        {
          options.MapInboundClaims = false;

          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,

            ValidateAudience = true,
            ValidAudience = settings.Audience,

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            ValidateLifetime = true,

            RequireExpirationTime = true,
            RequireSignedTokens = true,

            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            ClockSkew = TimeSpan.FromSeconds(30),
          };

          options.Events = new JwtBearerEvents
          {
            OnTokenValidated = async context =>
            {
              var subjectId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

              var sessionId = context
                .Principal?.FindFirst(AuthoraJwtDefaults.SessionIdClaim)
                ?.Value;

              if (string.IsNullOrWhiteSpace(subjectId))
              {
                context.Fail("Missing subject identifier.");

                return;
              }

              var resolver =
                context.HttpContext.RequestServices.GetRequiredService<IAuthoraSubjectResolver>();

              var subject = await resolver.ResolveAsync(
                subjectId,
                context.HttpContext.RequestAborted
              );

              if (
                subject is null
                || !string.Equals(subject.Id, subjectId, StringComparison.Ordinal)
              )
              {
                context.Fail("The user is unavailable.");

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
                AuthoraJwtDefaults.Scheme,
                ClaimTypes.Name,
                ClaimTypes.Role
              );

              if (
                !string.IsNullOrWhiteSpace(sessionId)
                && Guid.TryParseExact(sessionId, "N", out var parsedId)
              )
              {
                identity.AddClaim(
                  new Claim(AuthoraJwtDefaults.SessionIdClaim, parsedId.ToString("N"))
                );
              }

              context.Principal = new ClaimsPrincipal(identity);

              context.Principal = new ClaimsPrincipal(identity);
            },
          };
        }
      );

    services.AddAuthorization();

    return services;
  }
}
