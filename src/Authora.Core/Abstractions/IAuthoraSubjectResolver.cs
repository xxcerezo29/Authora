using System.Security.Claims;

namespace Authora.Core.Abstractions;

/// <summary>
/// Represents an application user and the current claims Authora may authenticate.
/// </summary>
/// <param name="Id">Stable application identifier for this user.</param>
/// <param name="Claims">Application claims to include after Authora removes reserved claims.</param>
public sealed record AuthoraSubject(string Id, IReadOnlyCollection<Claim> Claims);

/// <summary>
/// Defines how Authora resolves application users and their current claims.
/// </summary>
public interface IAuthoraSubjectResolver
{
  /// <summary>Resolves the current application subject and its claims, or returns null when unavailable.</summary>
  Task<AuthoraSubject?> ResolveAsync(
    string subjectId,
    CancellationToken cancellationToken = default
  );
}
