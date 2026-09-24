using Authora.Core.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
/// Defines persistence operations for personal access tokens.
/// </summary>
public interface IAuthoraTokenStore
{
  /// <summary>Persists a personal access token record.</summary>
  Task CreateAsync(PersonalAccessToken token, CancellationToken cancellationToken = default);

  /// <summary>Finds a personal access token by its identifier.</summary>
  Task<PersonalAccessToken?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>Revokes a token when it belongs to the specified subject.</summary>
  Task<bool> RevokeAsync(
    Guid tokenId,
    string subjectId,
    DateTimeOffset revokedAt,
    CancellationToken cancellationToken = default
  );
}
