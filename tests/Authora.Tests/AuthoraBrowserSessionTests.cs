using System.Security.Claims;
using System.Net.Http.Json;
using Authora.AspNetCore.Extensions;
using Authora.AspNetCore.Session;
using Authora.AspNetCore.Authorization;
using Authora.AspNetCore.Authentication;
using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authora.Tests;

public sealed class AuthoraBrowserSessionTests
{
  [Fact]
  public async Task TokenScheme_AuthenticatesBearerAndEnforcesAbility()
  {
    using var host = CreateHostAsync();
    using var client = CreateClient(host);
    using var scope = host.Services.CreateScope();
    var tokens = scope.ServiceProvider.GetRequiredService<AuthoraTokenService>();
    var created = await tokens.CreateAsync("user-1", "test client", ["profile:read"]);

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);

    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/profile");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", created.PlainTextToken);
    Assert.Equal(System.Net.HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);

    using var deniedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin");
    deniedRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", created.PlainTextToken);
    Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.SendAsync(deniedRequest)).StatusCode);
  }

  [Fact]
  public async Task SessionEndpoints_RequireLoginAndSignOutRevokesSession()
  {
    using var host = CreateHostAsync();
    using var client = CreateClient(host);

    var anonymous = await client.GetAsync("/me");
    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);

    var csrf = await client.GetFromJsonAsync<CsrfResponse>("/csrf");
    using var invalidLogin = new HttpRequestMessage(HttpMethod.Post, "/login");
    invalidLogin.Headers.Add("X-Authora-CSRF", "invalid-request-token");
    Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.SendAsync(invalidLogin)).StatusCode);

    using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/login");
    loginRequest.Headers.Add("X-Authora-CSRF", csrf!.RequestToken!);
    var login = await client.SendAsync(loginRequest);
    Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
    var authCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value =>
      value.StartsWith("__Host-Authora=", StringComparison.Ordinal)
      && !value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)
    );
    Assert.Contains("HttpOnly", authCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Secure", authCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("SameSite=Lax", authCookie, StringComparison.OrdinalIgnoreCase);

    var me = await client.GetAsync("/me");
    Assert.Equal(System.Net.HttpStatusCode.OK, me.StatusCode);
    Assert.Equal("user-1", await me.Content.ReadAsStringAsync());

    var sessionCsrf = await client.GetFromJsonAsync<CsrfResponse>("/csrf");
    using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/logout");
    logoutRequest.Headers.Add("X-Authora-CSRF", sessionCsrf!.RequestToken!);
    var logout = await client.SendAsync(logoutRequest);
    Assert.True(logout.StatusCode == System.Net.HttpStatusCode.NoContent, await logout.Content.ReadAsStringAsync());
    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/me")).StatusCode);
  }

  [Fact]
  public async Task ExistingSession_IsRejectedWhenSubjectIsUnavailable()
  {
    using var host = CreateHostAsync();
    using var client = CreateClient(host);
    var csrf = await client.GetFromJsonAsync<CsrfResponse>("/csrf");
    using var login = new HttpRequestMessage(HttpMethod.Post, "/login");
    login.Headers.Add("X-Authora-CSRF", csrf!.RequestToken!);
    Assert.Equal(System.Net.HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

    var resolver = Assert.IsType<TestSubjectResolver>(host.Services.GetRequiredService<IAuthoraSubjectResolver>());
    resolver.Available = false;

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/me")).StatusCode);
  }

  [Fact]
  public async Task CookieAuthenticatedUnsafeRequest_RequiresValidCsrfToken()
  {
    using var host = CreateHostAsync();
    using var client = CreateClient(host);

    var loginCsrf = await client.GetFromJsonAsync<CsrfResponse>("/csrf");
    using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/login");
    loginRequest.Headers.Add("X-Authora-CSRF", loginCsrf!.RequestToken!);
    await client.SendAsync(loginRequest);
    var rejected = await client.PostAsync("/change", content: null);
    Assert.Equal(System.Net.HttpStatusCode.BadRequest, rejected.StatusCode);

    var csrf = await client.GetFromJsonAsync<CsrfResponse>("/csrf");
    Assert.NotNull(csrf?.RequestToken);
    var request = new HttpRequestMessage(HttpMethod.Post, "/change");
    request.Headers.Add("X-Authora-CSRF", csrf!.RequestToken);
    var accepted = await client.SendAsync(request);

    Assert.True(accepted.StatusCode == System.Net.HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
  }

  private static HttpClient CreateClient(IHost host)
  {
    return new HttpClient(new CookiePersistenceHandler(host.GetTestServer().CreateHandler()))
    {
      BaseAddress = new Uri("https://localhost"),
    };
  }

  private static IHost CreateHostAsync()
  {
    var builder = Host.CreateDefaultBuilder()
      .ConfigureLogging(logging => logging.ClearProviders())
      .ConfigureWebHost(web =>
        web.UseTestServer()
          .ConfigureServices(services =>
          {
            services.AddRouting();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<IAuthoraSubjectResolver>(new TestSubjectResolver());
            services.AddSingleton<IAuthoraBrowserSessionStore>(new TestBrowserSessionStore());
            services.AddSingleton<IAuthoraTokenStore>(new TestTokenStore());
            services.AddAuthoraTokens();
            services.AddAuthoraBrowserSessions();
          })
          .Configure(app =>
          {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthoraSessionCsrf();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
              endpoints.MapGet("/csrf", (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
              {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Json(new CsrfResponse(tokens.RequestToken));
              });
              endpoints.MapPost("/login", async (HttpContext context, AuthoraBrowserSessionService sessions) =>
              {
                await sessions.SignInAsync(context, "user-1");
                return Results.Ok();
              }).AllowAnonymous().RequireAuthoraCsrf();
              endpoints.MapGet("/me", (ClaimsPrincipal principal) =>
                Results.Text(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "missing"))
                .RequireAuthoraSession();
              endpoints.MapPost("/logout", async (HttpContext context, AuthoraBrowserSessionService sessions) =>
              {
                await sessions.SignOutAsync(context);
                return Results.NoContent();
              }).RequireAuthoraSession();
              endpoints.MapPost("/change", () => Results.Ok()).RequireAuthoraSession();
              endpoints.MapGet("/api/profile", (ClaimsPrincipal principal) => Results.Text(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "missing"))
                .RequireAuthoraAbility("profile:read");
              endpoints.MapGet("/api/admin", () => Results.Ok()).RequireAuthoraAbility("admin");
            });
          })
      );

    var host = builder.Start();
    return host;
  }

  private sealed record CsrfResponse(string? RequestToken);

  private sealed class CookiePersistenceHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
  {
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (_cookies.Count > 0)
      {
        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _cookies.Select(pair => $"{pair.Key}={pair.Value}")));
      }

      var response = await base.SendAsync(request, cancellationToken);
      if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
      {
        foreach (var header in setCookies)
        {
          var pair = header.Split(';', 2)[0].Split('=', 2);
          if (pair.Length == 2) _cookies[pair[0]] = pair[1];
        }
      }

      return response;
    }
  }

  private sealed class TestSubjectResolver : IAuthoraSubjectResolver
  {
    public bool Available { get; set; } = true;

    public Task<AuthoraSubject?> ResolveAsync(string subjectId, CancellationToken cancellationToken = default)
    {
      if (!Available || subjectId != "user-1") return Task.FromResult<AuthoraSubject?>(null);
      return Task.FromResult<AuthoraSubject?>(new AuthoraSubject(subjectId, [new Claim(ClaimTypes.Name, "User One")]));
    }
  }

  private sealed class TestBrowserSessionStore : IAuthoraBrowserSessionStore
  {
    private readonly Dictionary<string, AuthoraBrowserSession> _sessions = new();
    private readonly HashSet<string> _revoked = new();

    public Task<string> CreateAsync(string subjectId, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
      var id = Guid.NewGuid().ToString("N");
      _sessions[id] = AuthoraBrowserSession.Create(id, subjectId, createdAt, expiresAt);
      return Task.FromResult(id);
    }

    public Task<AuthoraBrowserSession?> FindAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
      _sessions.TryGetValue(sessionKey, out var session);
      return Task.FromResult(session is not null && !_revoked.Contains(sessionKey) ? session : null);
    }

    public Task RevokeAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
      _revoked.Add(sessionKey);
      return Task.CompletedTask;
    }
  }

  private sealed class TestTokenStore : IAuthoraTokenStore
  {
    private readonly Dictionary<Guid, PersonalAccessToken> _tokens = new();
    private readonly HashSet<Guid> _revoked = new();

    public Task CreateAsync(PersonalAccessToken token, CancellationToken cancellationToken = default)
    {
      _tokens.Add(token.Id, token);
      return Task.CompletedTask;
    }

    public Task<PersonalAccessToken?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
      _tokens.TryGetValue(id, out var token);
      return Task.FromResult(token is not null && !_revoked.Contains(id) ? token : null);
    }

    public Task<bool> RevokeAsync(Guid tokenId, string subjectId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
      return Task.FromResult(_tokens.TryGetValue(tokenId, out var token) && token.SubjectId == subjectId && _revoked.Add(tokenId));
    }
  }
}
