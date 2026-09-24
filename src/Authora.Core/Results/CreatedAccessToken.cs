namespace Authora.Core.Results;

/// <summary>
/// Represents a newly created personal access token, including its one-time plaintext value.
/// </summary>
/// <param name="Id">Unique identifier used for later revocation.</param>
/// <param name="PlainTextToken">Plaintext bearer credential; store securely because it cannot be retrieved again.</param>
/// <param name="ExpiresAt">UTC time after which authentication fails.</param>
public sealed record CreatedAccessToken(Guid Id, string PlainTextToken, DateTimeOffset ExpiresAt);
