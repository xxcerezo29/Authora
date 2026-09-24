using System.Security.Claims;
using Authora.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.AspNetCore.Session;

/// <summary>
/// Stores browser authentication tickets through the registered session store.
/// </summary>
public sealed class AuthoraTicketStore : ITicketStore
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly TimeProvider _clock;

  /// <summary>
  /// Initializes a new instance of AuthoraTicketStore.
  /// </summary>
  public AuthoraTicketStore(IServiceScopeFactory scopeFactory, TimeProvider clock)
  {
    _scopeFactory = scopeFactory;
    _clock = clock;
  }

  /// <summary>
  /// Persists a new session ticket and returns its opaque key.
  /// </summary>
  public async Task<string> StoreAsync(AuthenticationTicket ticket)
  {
    var subjectId = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(subjectId))
    {
      throw new InvalidOperationException("Authentication ticket has no subject identifier.");
    }

    var now = _clock.GetUtcNow();

    var expiresAt =
      ticket.Properties.ExpiresUtc
      ?? throw new InvalidOperationException("Authentication ticket requires an expiration.");

    await using var scope = _scopeFactory.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IAuthoraBrowserSessionStore>();

    return await store.CreateAsync(subjectId, now, expiresAt);
  }

  /// <summary>
  /// Loads an active session ticket by its opaque key.
  /// </summary>
  public async Task<AuthenticationTicket?> RetrieveAsync(string key)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IAuthoraBrowserSessionStore>();

    var session = await store.FindAsync(key);

    if (session is null)
      return null;

    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, session.SubjectId) };

    var identity = new ClaimsIdentity(claims, AuthoraSessionDefaults.Scheme);

    var principal = new ClaimsPrincipal(identity);

    var properties = new AuthenticationProperties
    {
      IssuedUtc = session.CreatedAt,
      ExpiresUtc = session.ExpiresAt,
      IsPersistent = false,
    };

    return new AuthenticationTicket(principal, properties, AuthoraSessionDefaults.Scheme);
  }

  /// <summary>
  /// Keeps the fixed session expiration when ASP.NET Core renews a ticket.
  /// </summary>
  public Task RenewAsync(string key, AuthenticationTicket ticket)
  {
    // Authora v0.1 uses fixed-expiration sessions.
    // Renewal does not extend the database expiration.
    return Task.CompletedTask;
  }

  /// <summary>
  /// Removes and revokes the stored session ticket.
  /// </summary>
  public async Task RemoveAsync(string key)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IAuthoraBrowserSessionStore>();

    await store.RevokeAsync(key);
  }
}
