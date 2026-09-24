using System.Security.Claims;
using Authora.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Authora.AspNetCore.Session;

/// <summary>
/// Signs users into and out of database-backed browser sessions.
/// </summary>
public sealed class AuthoraBrowserSessionService
{
  private readonly IAuthoraSubjectResolver _subjects;
  private readonly TimeProvider _clock;
  private readonly AuthoraBrowserOptions _options;

  /// <summary>
  /// Initializes a new instance of AuthoraBrowserSessionService.
  /// </summary>
  public AuthoraBrowserSessionService(
    IAuthoraSubjectResolver subjects,
    TimeProvider clock,
    AuthoraBrowserOptions options
  )
  {
    _subjects = subjects;
    _clock = clock;
    _options = options;
  }

  /// <summary>
  /// Replaces the current browser session with a session for an available subject.
  /// </summary>
  /// <param name="context">The current HTTP request context.</param>
  /// <param name="subjectId">Identifier of the subject to sign in.</param>
  /// <param name="cancellationToken">Token used to cancel subject resolution.</param>
  /// <exception cref="InvalidOperationException">The subject cannot be resolved.</exception>
  public async Task SignInAsync(
    HttpContext context,
    string subjectId,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

    var subject = await _subjects.ResolveAsync(subjectId, cancellationToken);

    if (subject is null || !string.Equals(subject.Id, subjectId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("The subject is not available.");
    }

    var identity = new ClaimsIdentity(
      [new Claim(ClaimTypes.NameIdentifier, subject.Id)],
      AuthoraSessionDefaults.Scheme
    );

    var principal = new ClaimsPrincipal(identity);

    var properties = new AuthenticationProperties
    {
      IsPersistent = false,

      ExpiresUtc = _clock.GetUtcNow().Add(_options.SessionLifetime),
    };

    // Replace any existing browser session.
    await context.SignOutAsync(AuthoraSessionDefaults.Scheme);

    await context.SignInAsync(AuthoraSessionDefaults.Scheme, principal, properties);
  }

  /// <summary>
  /// Signs out the current browser session and revokes its server-side ticket.
  /// </summary>
  /// <param name="context">The current HTTP request context.</param>
  public Task SignOutAsync(HttpContext context)
  {
    return context.SignOutAsync(AuthoraSessionDefaults.Scheme);
  }
}
