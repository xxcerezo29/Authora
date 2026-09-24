# Optional JWT sessions

`Authora.Jwt` is an optional token-based mode for clients that cannot use the browser cookie flow. It issues short-lived signed JWT access tokens and opaque rotating refresh tokens backed by revocable sessions. Personal access tokens remain a separate mechanism with per-token abilities.

## Configuration

Register JWT validation, JWT sessions, persistence, and your subject resolver. Use an application-managed secret key that decodes from Base64 to at least 32 bytes; keep it outside source control and plan rotation before production deployment.

```csharp
builder.Services.AddAuthoraJwt(options =>
{
    options.Issuer = configuration["Authora:Jwt:Issuer"]!;
    options.Audience = configuration["Authora:Jwt:Audience"]!;
    options.SigningKeyBase64 = configuration["Authora:Jwt:SigningKeyBase64"]!;
    options.AccessTokenLifetime = TimeSpan.FromMinutes(15);
});
builder.Services.AddAuthoraJwtSessions(options =>
{
    options.RefreshTokenLifetime = TimeSpan.FromDays(7);
    options.SessionLifetime = TimeSpan.FromDays(30);
});
builder.Services.AddAuthoraEntityFrameworkCore<AppDbContext>();
builder.Services.AddScoped<IAuthoraSubjectResolver, AppSubjectResolver>();
```

Protect a route with `AuthorizeAttribute` and `AuthoraJwtDefaults.Scheme`. After application credential validation, call `AuthoraJwtSessionService.SignInAsync(subjectId)` and return its access and refresh tokens. Call `RefreshAsync` with the refresh token to rotate it; store the replacement securely and discard the old value. A correctly replayed consumed refresh token revokes its session. Revoke sessions with `RevokeSessionAsync(sessionId, subjectId)` during logout or account security events.

Access tokens expire independently of refresh tokens. Keep access lifetimes short, secure refresh tokens like passwords, and use HTTPS. The demo credentials and configuration in `Authora.SampleApi` are for local development only.

