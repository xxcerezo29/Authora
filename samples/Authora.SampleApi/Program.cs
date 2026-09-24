using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Authora.AspNetCore.Authentication;
using Authora.AspNetCore.Authorization;
using Authora.AspNetCore.Extensions;
using Authora.AspNetCore.Session;
using Authora.Core.Abstractions;
using Authora.Core.Services;
using Authora.EntityFrameworkCore.Extensions;
using Authora.Jwt.Authentication;
using Authora.Jwt.Extensions;
using Authora.Jwt.Services;
using Authora.SampleApi.Data;
using Authora.SampleApi.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
{
  options.UseSqlite("Data Source=authora-demo.db");
});

// Authora authentication
builder.Services.AddAuthoraTokens(options =>
{
  options.TokenLifetime = TimeSpan.FromDays(30);
});

builder.Services.AddAuthoraJwt(options =>
{
  options.Issuer = builder.Configuration["Authora:Jwt:Issuer"]!;

  options.Audience = builder.Configuration["Authora:Jwt:Audience"]!;

  options.SigningKeyBase64 = builder.Configuration["Authora:Jwt:SigningKeyBase64"]!;

  options.AccessTokenLifetime = TimeSpan.FromMinutes(15);
});

builder.Services.AddAuthoraJwtSessions(options =>
{
  options.RefreshTokenLifetime = TimeSpan.FromDays(7);

  options.SessionLifetime = TimeSpan.FromDays(30);
});

builder.Services.AddAuthoraBrowserSessions(options =>
{
  options.SessionLifetime = TimeSpan.FromHours(8);
});

// Authora EF Core persistence
builder.Services.AddAuthoraEntityFrameworkCore<AppDbContext>();

// Application-specific user resolver
builder.Services.AddScoped<IAuthoraSubjectResolver, DemoSubjectResolver>();

var app = builder.Build();

app.UseAuthentication();

app.UseAuthoraSessionCsrf();

app.UseAuthorization();

// Local development demonstration login.
// Never expose this example endpoint in production.
if (app.Environment.IsDevelopment())
{
  app.MapPost(
    "/dev/login",
    async (DemoLogin request, AuthoraTokenService tokens, CancellationToken cancellationToken) =>
    {
      var expectedPassword = Environment.GetEnvironmentVariable("AUTHORA_DEMO_PASSWORD");

      if (string.IsNullOrWhiteSpace(expectedPassword))
      {
        return Results.Problem("AUTHORA_DEMO_PASSWORD is not configured.");
      }

      var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? ""));

      var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedPassword));

      if (
        request.Username != "demo"
        || !CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
      )
      {
        return Results.Unauthorized();
      }

      var token = await tokens.CreateAsync(
        subjectId: "demo-user",
        name: "Development Client",
        abilities: ["profile:read"],
        cancellationToken: cancellationToken
      );

      return Results.Ok(new { token.PlainTextToken, token.ExpiresAt });
    }
  );
}

// Protected endpoint
app.MapGet(
    "/api/me",
    (ClaimsPrincipal user) =>
    {
      return Results.Ok(
        new
        {
          Id = user.FindFirstValue(ClaimTypes.NameIdentifier),

          Name = user.Identity?.Name,
        }
      );
    }
  )
  .RequireAuthoraAbility("profile:read");

// Revoke the credential used for this request
app.MapPost(
    "/api/logout",
    async (ClaimsPrincipal user, AuthoraTokenService tokens, CancellationToken cancellationToken) =>
    {
      var tokenId = user.FindFirstValue(AuthoraDefaults.TokenIdClaim);

      var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);

      if (!Guid.TryParseExact(tokenId, "N", out var id) || string.IsNullOrWhiteSpace(subjectId))
      {
        return Results.Unauthorized();
      }

      await tokens.RevokeAsync(id, subjectId, cancellationToken);

      return Results.NoContent();
    }
  )
  .RequireAuthorization();

app.MapPost(
  "/dev/jwt/login",
  async (
    DemoLogin request,
    AuthoraJwtSessionService authora,
    CancellationToken cancellationToken
  ) =>
  {
    var expectedPassword = Environment.GetEnvironmentVariable("AUTHORA_DEMO_PASSWORD");

    if (string.IsNullOrWhiteSpace(expectedPassword))
    {
      return Results.Problem("Demo password is not configured.");
    }

    var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? ""));

    var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedPassword));

    if (
      request.Username != "demo"
      || !CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
    )
    {
      return Results.Unauthorized();
    }

    var result = await authora.SignInAsync("demo-user", cancellationToken);

    return Results.Ok(result);
  }
);

app.MapPost(
  "/auth/jwt/refresh",
  async (
    RefreshRequest request,
    AuthoraJwtSessionService authora,
    CancellationToken cancellationToken
  ) =>
  {
    var result = await authora.RefreshAsync(request.RefreshToken, cancellationToken);

    if (result is null)
      return Results.Unauthorized();

    return Results.Ok(result);
  }
);

app.MapPost(
    "/auth/jwt/logout",
    async (
      ClaimsPrincipal user,
      AuthoraJwtSessionService authora,
      CancellationToken cancellationToken
    ) =>
    {
      var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);

      var sessionIdValue = user.FindFirstValue(AuthoraJwtDefaults.SessionIdClaim);

      if (
        string.IsNullOrWhiteSpace(subjectId)
        || !Guid.TryParseExact(sessionIdValue, "N", out var sessionId)
      )
      {
        return Results.Unauthorized();
      }

      await authora.RevokeSessionAsync(sessionId, subjectId, cancellationToken);

      return Results.NoContent();
    }
  )
  .RequireAuthorization(
    new AuthorizeAttribute { AuthenticationSchemes = AuthoraJwtDefaults.Scheme }
  );

app.MapGet(
    "/dev/jwt/me",
    (ClaimsPrincipal user) =>
    {
      return Results.Ok(
        new
        {
          Id = user.FindFirstValue(ClaimTypes.NameIdentifier),

          Name = user.Identity?.Name,
        }
      );
    }
  )
  .RequireAuthorization(
    new AuthorizeAttribute { AuthenticationSchemes = AuthoraJwtDefaults.Scheme }
  );

app.MapGet(
  "/auth/session/csrf",
  (HttpContext context, IAntiforgery antiforgery) =>
  {
    var tokens = antiforgery.GetAndStoreTokens(context);

    return Results.Ok(new { RequestToken = tokens.RequestToken });
  }
);

if (app.Environment.IsDevelopment())
{
  app.MapPost(
      "/auth/session/login",
      async (DemoLogin request, HttpContext context, AuthoraBrowserSessionService authora) =>
      {
        var expectedPassword = Environment.GetEnvironmentVariable("AUTHORA_DEMO_PASSWORD");

        if (string.IsNullOrWhiteSpace(expectedPassword))
        {
          return Results.Problem("Demo password is not configured.");
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? ""));

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedPassword));

        if (
          request.Username != "demo"
          || !CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
        )
        {
          return Results.Unauthorized();
        }

        await authora.SignInAsync(context, "demo-user", context.RequestAborted);

        return Results.Ok(new { Message = "Login successful" });
      }
    )
    .RequireAuthoraCsrf();
}

app.MapGet(
    "/auth/session/me",
    (ClaimsPrincipal user) =>
    {
      return Results.Ok(
        new
        {
          Id = user.FindFirstValue(ClaimTypes.NameIdentifier),

          Name = user.Identity?.Name,

          Roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(),
        }
      );
    }
  )
  .RequireAuthoraSession();

app.MapPost(
    "/auth/session/logout",
    async (HttpContext context, AuthoraBrowserSessionService authora) =>
    {
      await authora.SignOutAsync(context);

      return Results.NoContent();
    }
  )
  .RequireAuthoraSession();

app.Run();

public sealed record DemoLogin(string Username, string Password);

public sealed record RefreshRequest(string RefreshToken);
