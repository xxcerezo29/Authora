# Authora

Authora is a .NET authentication library inspired by Laravel Sanctum. It gives ASP.NET Core applications two primary authentication patterns: personal access tokens for API clients and database-backed cookie sessions for first-party browser apps. A separate JWT package adds short-lived access tokens with rotating refresh tokens and revocable sessions.

Authora does not authenticate passwords or own your user database. Your application verifies credentials and implements `IAuthoraSubjectResolver` to map its users and claims into Authora.

## Packages

All projects target **.NET 10**.

| Project | Purpose |
| --- | --- |
| `Authora` | Umbrella package that installs all Authora modules |
| `Authora.Core` | Token/session entities, options, store contracts, and personal access token service |
| `Authora.AspNetCore` | ASP.NET Core bearer-token authentication, browser sessions, CSRF, and authorization helpers |
| `Authora.EntityFrameworkCore` | EF Core stores and model configurations |
| `Authora.Jwt` | Optional JWT access and rotating refresh token sessions |
| `Authora.SampleApi` | Local demonstration of all three authentication patterns |

The sample uses SQLite and development-only login endpoints. Do not deploy those credential endpoints as production login flows.

## Getting started

Add the `Authora` NuGet package to install the complete suite, or reference individual packages when you only need specific features. For a typical persistent ASP.NET Core API, reference `Authora.AspNetCore` and `Authora.EntityFrameworkCore`; add `Authora.Jwt` only if the application uses JWT sessions. These packages are built for .NET 10.

When a GitHub Release is published, the release workflow builds and tests the source, creates versioned `.nupkg` files for the umbrella package and all four library packages, and attaches them to that release. The tag version is used as the NuGet package version, with an optional leading `v` removed. Packages are attached to GitHub Releases and are not published to nuget.org.

Register the store mappings in your EF Core context:

```csharp
using Authora.Core.Entities;
using Authora.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();
    public DbSet<AuthoraBrowserSession> BrowserSessions => Set<AuthoraBrowserSession>();
    public DbSet<AuthoraJwtSession> JwtSessions => Set<AuthoraJwtSession>();
    public DbSet<AuthoraRefreshToken> RefreshTokens => Set<AuthoraRefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PersonalAccessTokenConfiguration());
        modelBuilder.ApplyConfiguration(new AuthoraBrowserSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AuthoraJwtSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AuthoraRefreshTokenConfiguration());
    }
}
```

Register Authora after your `DbContext`, provide your user resolver, and configure authentication middleware:

```csharp
builder.Services.AddAuthoraTokens(options =>
    options.TokenLifetime = TimeSpan.FromDays(30));
builder.Services.AddAuthoraBrowserSessions(options =>
    options.SessionLifetime = TimeSpan.FromHours(8));
builder.Services.AddAuthoraEntityFrameworkCore<AppDbContext>();
builder.Services.AddScoped<IAuthoraSubjectResolver, AppSubjectResolver>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthoraSessionCsrf();
app.UseAuthorization();
```

The host application's login endpoint validates credentials and then creates a token or starts a browser session. See [personal access tokens](docs/personal-access-tokens.md) and [browser sessions](docs/browser-sessions.md) for complete examples and security notes.

## Database migrations

Authora provides EF model configurations; it does not run migrations for your application. Add the four `DbSet` properties and apply the configurations in your context, then generate and apply migrations using your application's normal EF Core workflow. Keep the database provider and context under application control.

## Security and application responsibilities

- Serve authentication traffic over HTTPS. Browser session and antiforgery cookies are configured Secure, HttpOnly, and SameSite Lax by default.
- Use `UseAuthoraSessionCsrf` after authentication middleware and before authorization middleware. It checks unsafe requests carrying the configured browser session cookie.
- Use `RequireAuthoraCsrf()` on login or other unsafe endpoints that need explicit antiforgery validation, including endpoints before a session exists.
- Configure credentialed CORS only for exact trusted origins. Never combine credentialed requests with wildcard origins.
- Keep signing and data protection keys out of source control, and plan their storage and rotation for your hosting environment.
- Revalidate subjects through `IAuthoraSubjectResolver`; Authora uses current application claims when authenticating requests.
- Store and return the plaintext personal access token only when it is created. Authora persists a hash for later authentication.

These library defaults do not replace your application's login, account recovery, rate limiting, origin policy, or deployment security decisions.

## Local checks

```powershell
dotnet restore Authora.slnx
dotnet build Authora.slnx --configuration Release --no-restore
dotnet test tests/Authora.Tests/Authora.Tests.csproj --configuration Release --no-build
```

## Documentation

- [Personal access tokens](docs/personal-access-tokens.md)
- [Browser sessions and CSRF](docs/browser-sessions.md)
- [JWT sessions](docs/jwt-sessions.md)
- [Contributing and Git workflow](CONTRIBUTING.md)
