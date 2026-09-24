using Authora.Core.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
/// Defines persistence operations for JWT sessions and rotating refresh tokens.
/// </summary>
public interface IAuthoraRefreshStore
{
  /// <summary>Stores a JWT session and its initial refresh token.</summary>
  Task CreateAsync(
    AuthoraJwtSession session,
    AuthoraRefreshToken token,
    CancellationToken cancellationToken = default
  );

  /// <summary>Finds a refresh token by its identifier.</summary>
  Task<AuthoraRefreshToken?> FindTokenAsync(
    Guid tokenId,
    CancellationToken cancellationToken = default
  );

  /// <summary>Finds a JWT session by its identifier.</summary>
  Task<AuthoraJwtSession?> FindSessionAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default
  );

  /// <summary>Atomically consumes a refresh token and stores its replacement.</summary>
  Task<bool> TryRotateAsync(
    Guid currentTokenId,
    AuthoraRefreshToken replacement,
    DateTimeOffset now,
    CancellationToken cancellationToken = default
  );

  /// <summary>Revokes a session only when it belongs to the specified subject.</summary>
  Task<bool> RevokeSessionAsync(
    Guid sessionId,
    string subjectId,
    DateTimeOffset now,
    CancellationToken cancellationToken = default
  );
}
