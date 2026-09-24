using System.Security.Cryptography;
using System.Text;
using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authora.EntityFrameworkCore.Repositories;

/// <summary>
/// Persists hashed browser session keys and checks their active lifetime.
/// </summary>
public sealed class EfAuthoraBrowserSessionStore<TDbContext> : IAuthoraBrowserSessionStore
  where TDbContext : DbContext
{
  private readonly TDbContext _context;
  private readonly TimeProvider _clock;

  /// <summary>
  /// Initializes a new instance of EfAuthoraBrowserSessionStore.
  /// </summary>
  public EfAuthoraBrowserSessionStore(TDbContext context, TimeProvider clock)
  {
    _context = context;
    _clock = clock;
  }

  /// <summary>
  /// Creates a session record with a random key and returns the key to the caller.
  /// </summary>
  public async Task<string> CreateAsync(
    string subjectId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

    if (subjectId.Length > 128)
      throw new ArgumentException("Subject ID exceeds 128 characters.", nameof(subjectId));

    if (expiresAt <= createdAt)
      throw new ArgumentException("Session expiration must be after creation.");

    var sessionKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    var session = AuthoraBrowserSession.Create(
      HashKey(sessionKey),
      subjectId,
      createdAt,
      expiresAt
    );

    await _context.Set<AuthoraBrowserSession>().AddAsync(session, cancellationToken);

    await _context.SaveChangesAsync(cancellationToken);

    return sessionKey;
  }

  /// <summary>
  /// Finds the session associated with a key when it has not expired or been revoked.
  /// </summary>
  public async Task<AuthoraBrowserSession?> FindAsync(
    string sessionKey,
    CancellationToken cancellationToken = default
  )
  {
    if (string.IsNullOrWhiteSpace(sessionKey) || sessionKey.Length != 64)
    {
      return null;
    }

    var keyHash = HashKey(sessionKey);

    var session = await _context
      .Set<AuthoraBrowserSession>()
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.IdHash == keyHash, cancellationToken);

    if (session is null || !session.IsActiveAt(_clock.GetUtcNow()))
    {
      return null;
    }

    return session;
  }

  /// <summary>
  /// Revokes the session associated with the supplied key.
  /// </summary>
  public async Task RevokeAsync(string sessionKey, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sessionKey) || sessionKey.Length != 64)
    {
      return;
    }

    var keyHash = HashKey(sessionKey);

    var session = await _context
      .Set<AuthoraBrowserSession>()
      .FirstOrDefaultAsync(x => x.IdHash == keyHash, cancellationToken);

    if (session is null)
      return;

    session.Revoke(_clock.GetUtcNow());

    await _context.SaveChangesAsync(cancellationToken);
  }

  private static string HashKey(string sessionKey)
  {
    var bytes = Encoding.UTF8.GetBytes(sessionKey);

    return Convert.ToHexString(SHA256.HashData(bytes));
  }
}
