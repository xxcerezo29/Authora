namespace Authora.Core.Entities;

/// <summary>
/// Represents a hashed refresh token and its rotation/consumption state.
/// </summary>
public sealed class AuthoraRefreshToken
{
  private AuthoraRefreshToken() { }

  /// <summary>
  /// Gets the unique refresh token identifier.
  /// </summary>
  public Guid Id { get; private set; }

  /// <summary>
  /// Gets the identifier of the owning JWT session.
  /// </summary>
  public Guid SessionId { get; private set; }

  /// <summary>
  /// Gets the hash of the refresh token secret.
  /// </summary>
  public string SecretHash { get; private set; } = null!;

  /// <summary>
  /// Gets the UTC time when the token was created.
  /// </summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>
  /// Gets the UTC expiration time.
  /// </summary>
  public DateTimeOffset ExpiresAt { get; private set; }

  /// <summary>
  /// Gets the UTC time when this token was consumed, or <see langword="null"/> while unused.
  /// </summary>
  public DateTimeOffset? ConsumedAt { get; private set; }

  /// <summary>
  /// Gets the replacement token ID after rotation, if consumed.
  /// </summary>
  public Guid? ReplacedById { get; private set; }

  /// <summary>
  /// Creates .
  /// </summary>
  public static AuthoraRefreshToken Create(
    Guid id,
    Guid sessionId,
    string secretHash,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt
  )
  {
    return new AuthoraRefreshToken
    {
      Id = id,
      SessionId = sessionId,
      SecretHash = secretHash,
      CreatedAt = createdAt,
      ExpiresAt = expiresAt,
    };
  }
}
