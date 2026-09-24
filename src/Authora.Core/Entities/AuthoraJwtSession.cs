namespace Authora.Core.Entities;

/// <summary>
/// Represents a revocable JWT login session and its fixed expiration.
/// </summary>
public sealed class AuthoraJwtSession
{
  private AuthoraJwtSession() { }

  /// <summary>
  /// Gets the unique session identifier.
  /// </summary>
  public Guid Id { get; private set; }

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
  public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

  /// <summary>
  /// Creates .
  /// </summary>
  public static AuthoraJwtSession Create(
    Guid id,
    string subjectId,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt
  )
  {
    return new AuthoraJwtSession
    {
      Id = id,
      SubjectId = subjectId,
      CreatedAt = createdAt,
      ExpiresAt = expiresAt,
    };
  }
}
