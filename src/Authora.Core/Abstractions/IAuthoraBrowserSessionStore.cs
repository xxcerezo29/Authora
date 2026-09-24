using Authora.Core.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
/// Defines persistence operations for server-side browser sessions.
/// </summary>
public interface IAuthoraBrowserSessionStore
{
  /// <summary>Creates a browser session and returns its opaque session key.</summary>
  Task<string> CreateAsync(
    string subjectId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken = default
  );

  /// <summary>Finds an active browser session using its opaque key.</summary>
  Task<AuthoraBrowserSession?> FindAsync(
    string sessionKey,
    CancellationToken cancellationToken = default
  );

  /// <summary>Revokes the browser session associated with the key.</summary>
  Task RevokeAsync(string sessionKey, CancellationToken cancellationToken = default);
}
