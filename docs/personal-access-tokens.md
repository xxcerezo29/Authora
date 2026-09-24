# Personal access tokens

Personal access tokens suit API clients, scripts, and integrations. A token is a random secret returned once by `AuthoraTokenService.CreateAsync`; Authora stores its SHA-256 hash and the token's abilities, owner, and expiration. Send it as `Authorization: Bearer <token>`.

## Create a token

Create tokens only after your application has verified the user's credentials and resolved the user according to its own account policy:

```csharp
app.MapPost("/tokens", async (
    CreateTokenRequest request,
    ClaimsPrincipal user,
    AuthoraTokenService tokens,
    CancellationToken cancellationToken) =>
{
    var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(subjectId)) return Results.Unauthorized();

    var created = await tokens.CreateAsync(
        subjectId,
        request.Name,
        ["profile:read"],
        cancellationToken);

    return Results.Ok(new { created.PlainTextToken, created.ExpiresAt });
}).RequireAuthorization();

public sealed record CreateTokenRequest(string Name);
```

The client must save `PlainTextToken` when it receives it; it cannot be retrieved from the database later. Keep token creation responses private and avoid logging token values.

## Protect endpoints with abilities

Register `AddAuthoraTokens` and `IAuthoraTokenStore` persistence, then use the bearer authentication scheme and ability policy:

```csharp
app.MapGet("/api/profile", (ClaimsPrincipal user) =>
    Results.Ok(user.FindFirstValue(ClaimTypes.NameIdentifier)))
    .RequireAuthoraAbility("profile:read");
```

Abilities are exact string claims. The `*` ability grants access to any required ability. Decide who can issue wildcard tokens and keep abilities narrow by default.

## Revoke a token

The application can revoke a token by its `CreatedAccessToken.Id` and owner subject ID. To revoke the credential used for the current request, read `AuthoraDefaults.TokenIdClaim` and the `ClaimTypes.NameIdentifier` claim, validate the token ID, and call `AuthoraTokenService.RevokeAsync(id, subjectId, cancellationToken)`. Revocation is scoped to the owner.

## Storage and expiry

The EF store saves token metadata and a hash, never the plaintext secret. The default token lifetime is 30 days; set `AuthoraOptions.TokenLifetime` to a positive duration appropriate for the client. Keep ability and expiration policies in the host application and provide a user-facing way to inspect and revoke active credentials.

