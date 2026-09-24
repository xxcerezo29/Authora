namespace Authora.Core.Entities;

/// <summary>
/// Represents a personal access token record; only its secret hash is persisted.
/// </summary>
public sealed class PersonalAccessToken
{
    private PersonalAccessToken()
    {
        // Required by EF Core.
    }

    /// <summary>
    /// Gets the unique token identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who owns this token.
    /// </summary>
    public string SubjectId { get; private set; } = null!;

    /// <summary>
    /// Gets the user-visible client or device name.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Gets the SHA-256 hash of the secret; plaintext is never persisted.
    /// </summary>
    public string SecretHash { get; private set; } = null!;

    /// <summary>
    /// Gets the JSON-serialized abilities granted to this token.
    /// </summary>
    public string AbilitiesJson { get; private set; } = "[]";

    /// <summary>
    /// Gets the UTC time when the token was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC expiration time, if one was configured.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the UTC revocation time, or <see langword="null"/> while active.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Indicates whether the token is not revoked and has not expired at the supplied time.
    /// </summary>
    public bool IsValidAt(DateTimeOffset now)
    {
        return RevokedAt is null &&
               (ExpiresAt is null || ExpiresAt > now);
    }

    /// <summary>
    /// Creates a token persistence record.
    /// </summary>
    public static PersonalAccessToken Create(
        Guid id,
        string subjectId,
        string name,
        string secretHash,
        string abilitiesJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        return new PersonalAccessToken
        {
            Id = id,
            SubjectId = subjectId,
            Name = name,
            SecretHash = secretHash,
            AbilitiesJson = abilitiesJson,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }
}
