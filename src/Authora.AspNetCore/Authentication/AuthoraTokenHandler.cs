using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Authora.Core.Abstractions;
using Authora.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.AspNetCore.Authentication;

/// <summary>
/// Authenticates bearer credentials issued as Authora personal access tokens.
/// </summary>
public sealed class AuthoraTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  private readonly AuthoraTokenService _tokens;
  private readonly IAuthoraSubjectResolver _subjects;

  /// <summary>
  /// Initializes a new instance of AuthoraTokenHandler.
  /// </summary>
  public AuthoraTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthoraTokenService tokens,
    IAuthoraSubjectResolver subjects
  )
    : base(options, logger, encoder)
  {
    _tokens = tokens;
    _subjects = subjects;
  }

  /// <summary>Validates the bearer token and builds an authenticated principal.</summary>
  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var authorization = Request.Headers.Authorization.ToString();

    if (string.IsNullOrWhiteSpace(authorization))
      return AuthenticateResult.NoResult();

    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
      return AuthenticateResult.NoResult();
    }

    var plainTextToken = authorization["Bearer ".Length..].Trim();

    var token = await _tokens.AuthenticateAsync(plainTextToken, Context.RequestAborted);

    if (token is null)
    {
      return AuthenticateResult.Fail("Invalid or expired access token.");
    }

    var subject = await _subjects.ResolveAsync(token.SubjectId, Context.RequestAborted);

    if (subject is null || !string.Equals(subject.Id, token.SubjectId, StringComparison.Ordinal))
    {
      return AuthenticateResult.Fail("The associated user is unavailable.");
    }

    // Prevent application-provided claims from
    // overriding Authora's trusted credential claims.
    var userClaims = subject.Claims.Where(claim =>
      claim.Type != ClaimTypes.NameIdentifier
      && !claim.Type.StartsWith("authora:", StringComparison.Ordinal)
    );

    var identity = new ClaimsIdentity(userClaims, Scheme.Name);

    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject.Id));

    identity.AddClaim(new Claim(AuthoraDefaults.TokenIdClaim, token.Id.ToString("N")));

    var abilities = JsonSerializer.Deserialize<string[]>(token.AbilitiesJson) ?? [];

    foreach (var ability in abilities)
    {
      identity.AddClaim(new Claim(AuthoraDefaults.AbilityClaim, ability));
    }

    var principal = new ClaimsPrincipal(identity);

    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    return AuthenticateResult.Success(ticket);
  }

  /// <summary>Returns a bearer authentication challenge without redirecting.</summary>
  protected override Task HandleChallengeAsync(AuthenticationProperties properties)
  {
    Response.Headers["WWW-Authenticate"] = "Bearer";
    Response.StatusCode = 401;

    return Task.CompletedTask;
  }
}
