# Browser sessions and CSRF

Authora browser sessions use an ASP.NET Core cookie containing a reference to a server-side session. The store retains the opaque session key as a hash. On each authenticated request, Authora resolves the current subject again and uses its current claims.

## Setup

Register the application database, browser sessions, EF stores, and subject resolver. Add middleware in this order:

```csharp
builder.Services.AddAuthoraBrowserSessions(options =>
    options.SessionLifetime = TimeSpan.FromHours(8));
builder.Services.AddAuthoraEntityFrameworkCore<AppDbContext>();
builder.Services.AddScoped<IAuthoraSubjectResolver, AppSubjectResolver>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthoraSessionCsrf();
app.UseAuthorization();
```

After validating the login credentials in your application, start the session:

```csharp
app.MapPost("/auth/session/login", async (
    HttpContext context,
    LoginRequest request,
    ILoginService login,
    AuthoraBrowserSessionService sessions,
    CancellationToken cancellationToken) =>
{
    var subjectId = await login.ValidateAsync(request, cancellationToken);
    if (subjectId is null) return Results.Unauthorized();

    await sessions.SignInAsync(context, subjectId, cancellationToken);
    return Results.NoContent();
}).AllowAnonymous().RequireAuthoraCsrf();

app.MapGet("/auth/session/me", (ClaimsPrincipal user) =>
    Results.Ok(user.FindFirstValue(ClaimTypes.NameIdentifier)))
    .RequireAuthoraSession();

app.MapPost("/auth/session/logout", async (
    HttpContext context,
    AuthoraBrowserSessionService sessions) =>
{
    await sessions.SignOutAsync(context);
    return Results.NoContent();
}).RequireAuthoraSession();
```

`LoginRequest` and `ILoginService` above are application types. Authora does not validate passwords.

## CSRF and cross-origin browser apps

`AddAuthoraBrowserSessions` configures antiforgery with the `X-Authora-CSRF` request header and `__Host-Authora.Csrf` cookie. Expose a same-origin endpoint that calls `IAntiforgery.GetAndStoreTokens(context)` and returns `RequestToken`; clients send that value in the configured header for unsafe requests. Add `.RequireAuthoraCsrf()` to login and other unsafe endpoints that need explicit validation before a session cookie exists.

```csharp
app.MapGet("/auth/session/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { tokens.RequestToken });
});
```

`UseAuthoraSessionCsrf` validates unsafe methods when the request contains the configured Authora session cookie. Safe methods such as GET and HEAD are not checked. Keep state-changing operations on unsafe HTTP methods.

For a separate SPA origin on the same site (such as two HTTPS subdomains), configure exact trusted CORS origins and allow credentials and the antiforgery header. The SPA must send credentials and the antiforgery header. Do not use wildcard origins with credentials. The default SameSite Lax cookie is not sent by browsers on cross-site fetch requests. Prefer same-site deployment; if cross-site cookies are required, configure both cookies as `SameSite=None` while retaining Secure:

```csharp
builder.Services.Configure<CookieAuthenticationOptions>(AuthoraSessionDefaults.Scheme, options =>
    options.Cookie.SameSite = SameSiteMode.None);
builder.Services.Configure<AntiforgeryOptions>(options =>
    options.Cookie.SameSite = SameSiteMode.None);
```

Test the resulting browser cookie behavior in the intended domain setup.

## Cookie and deployment settings

The default cookie is Secure, HttpOnly, SameSite Lax, path `/`, and has a fixed expiration. Serve the app over HTTPS. If you change cookie name, domain, SameSite, or lifetime, keep CSRF and CORS settings aligned with the browser deployment. Configure ASP.NET Core Data Protection key storage and protection for multi-instance hosting; Authora does not choose that storage for you.
