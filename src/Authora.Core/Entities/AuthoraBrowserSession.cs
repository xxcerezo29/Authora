namespace Authora.Core.Entities;

/// <summary>
/// Represents a server-side browser session whose client key is stored as a hash.
/// </summary>
public sealed class AuthoraBrowserSession
{
  private AuthoraBrowserSession() { }

  /// <summary>
  /// Gets the hash of the opaque browser session key.
  /// </summary>
  public string IdHash { get; private set; } = null!;

  /// <summary>
  /// Gets the identifier of the user who owns this session.
  /// </summary>
  public string SubjectId { get; private set; } = null!;

  /// <summary>
  /// Gets the UTC time when the session was created.
  /// </summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>
  /// Gets the UTC session expiration time.
  /// </summary>
  public DateTimeOffset ExpiresAt { get; private set; }

  /// <summary>
  /// Gets the UTC revocation time, or <see langword="null"/> while active.
  /// </summary>
  public DateTimeOffset? RevokedAt { get; private set; }

  /// <summary>
  /// Indicates whether active at.
  /// </summary>
  public bool IsActiveAt(DateTimeOffset now)
  {
    return RevokedAt is null && ExpiresAt > now;
  }

  /// <summary>
  /// Revokes .
  /// </summary>
  public void Revoke(DateTimeOffset now)
  {
    if (RevokedAt is null)
      RevokedAt = now;
  }

  /// <summary>
  /// Creates .
  /// </summary>
  public static AuthoraBrowserSession Create(
    string idHash,
    string subjectId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt
  )
  {
    return new AuthoraBrowserSession
    {
      IdHash = idHash,
      SubjectId = subjectId,
      CreatedAt = createdAt,
      ExpiresAt = expiresAt,
    };
  }
}
