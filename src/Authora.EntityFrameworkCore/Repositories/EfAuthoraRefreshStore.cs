using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authora.EntityFrameworkCore.Repositories;

/// <summary>
/// Persists JWT sessions and atomically rotates their refresh tokens.
/// </summary>
public sealed class EfAuthoraRefreshStore<TDbContext> : IAuthoraRefreshStore
  where TDbContext : DbContext
{
  private readonly TDbContext _context;

  /// <summary>
  /// Initializes a new instance of EfAuthoraRefreshStore.
  /// </summary>
  public EfAuthoraRefreshStore(TDbContext context)
  {
    _context = context;
  }

  /// <summary>
  /// Adds the initial JWT session and refresh token in one save operation.
  /// </summary>
  public async Task CreateAsync(
    AuthoraJwtSession session,
    AuthoraRefreshToken token,
    CancellationToken cancellationToken = default
  )
  {
    _context.Set<AuthoraJwtSession>().Add(session);

    _context.Set<AuthoraRefreshToken>().Add(token);

    await _context.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Finds a refresh token by ID without tracking it for changes.
  /// </summary>
  public Task<AuthoraRefreshToken?> FindTokenAsync(
    Guid tokenId,
    CancellationToken cancellationToken = default
  )
  {
    return _context
      .Set<AuthoraRefreshToken>()
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == tokenId, cancellationToken);
  }

  /// <summary>
  /// Finds a JWT session by ID without tracking it for changes.
  /// </summary>
  public Task<AuthoraJwtSession?> FindSessionAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default
  )
  {
    return _context
      .Set<AuthoraJwtSession>()
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
  }

  /// <summary>
  /// Consumes an active refresh token and inserts its replacement atomically.
  /// </summary>
  public async Task<bool> TryRotateAsync(
    Guid currentTokenId,
    AuthoraRefreshToken replacement,
    DateTimeOffset now,
    CancellationToken cancellationToken = default
  )
  {
    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    var affectedRows = await _context
      .Set<AuthoraRefreshToken>()
      .Where(x =>
        x.Id == currentTokenId
        && x.SessionId == replacement.SessionId
        && x.ConsumedAt == null
        && x.ExpiresAt > now
        && _context
          .Set<AuthoraJwtSession>()
          .Any(session =>
            session.Id == x.SessionId && session.RevokedAt == null && session.ExpiresAt > now
          )
      )
      .ExecuteUpdateAsync(
        updates =>
          updates
            .SetProperty(x => x.ConsumedAt, (DateTimeOffset?)now)
            .SetProperty(x => x.ReplacedById, (Guid?)replacement.Id),
        cancellationToken
      );

    if (affectedRows != 1)
    {
      await transaction.RollbackAsync(cancellationToken);

      return false;
    }

    _context.Set<AuthoraRefreshToken>().Add(replacement);

    await _context.SaveChangesAsync(cancellationToken);

    await transaction.CommitAsync(cancellationToken);

    return true;
  }

  /// <summary>
  /// Revokes a session only when the supplied subject owns it.
  /// </summary>
  public async Task<bool> RevokeSessionAsync(
    Guid sessionId,
    string subjectId,
    DateTimeOffset now,
    CancellationToken cancellationToken = default
  )
  {
    var affectedRows = await _context
      .Set<AuthoraJwtSession>()
      .Where(x => x.Id == sessionId && x.SubjectId == subjectId && x.RevokedAt == null)
      .ExecuteUpdateAsync(
        updates => updates.SetProperty(x => x.RevokedAt, (DateTimeOffset?)now),
        cancellationToken
      );

    return affectedRows > 0;
  }
}
