using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authora.EntityFrameworkCore.Repositories;

/// <summary>
/// Persists personal access token records through EF Core.
/// </summary>
public sealed class EfAuthoraTokenStore<TDbContext> : IAuthoraTokenStore
  where TDbContext : DbContext
{
  private readonly TDbContext _context;

  /// <summary>
  /// Initializes a new instance of EfAuthoraTokenStore.
  /// </summary>
  public EfAuthoraTokenStore(TDbContext context)
  {
    _context = context;
  }

  /// <summary>
  /// Adds a token record to the database.
  /// </summary>
  public async Task CreateAsync(
    PersonalAccessToken token,
    CancellationToken cancellationToken = default
  )
  {
    await _context.Set<PersonalAccessToken>().AddAsync(token, cancellationToken);

    await _context.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Finds a token by ID without tracking it for changes.
  /// </summary>
  public Task<PersonalAccessToken?> FindByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default
  )
  {
    return _context
      .Set<PersonalAccessToken>()
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
  }

  /// <summary>
  /// Revokes an active token only when its subject ID matches the owner.
  /// </summary>
  public async Task<bool> RevokeAsync(
    Guid tokenId,
    string subjectId,
    DateTimeOffset revokedAt,
    CancellationToken cancellationToken = default
  )
  {
    var affectedRows = await _context
      .Set<PersonalAccessToken>()
      .Where(x => x.Id == tokenId && x.SubjectId == subjectId && x.RevokedAt == null)
      .ExecuteUpdateAsync(
        updates => updates.SetProperty(x => x.RevokedAt, (DateTimeOffset?)revokedAt),
        cancellationToken
      );

    return affectedRows > 0;
  }
}
