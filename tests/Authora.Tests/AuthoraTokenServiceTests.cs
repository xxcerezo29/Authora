using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.Core.Options;
using Authora.Core.Services;
using System.Text.Json;
using Authora.Tests.TestSupport;

namespace Authora.Tests;

public sealed class AuthoraTokenServiceTests
{
  private static AuthoraTokenService CreateService(IAuthoraTokenStore store)
  {
    return new AuthoraTokenService(
      store,
      TimeProvider.System,
      new AuthoraOptions { TokenLifetime = TimeSpan.FromHours(1) }
    );
  }

  [Fact]
  public async Task CreatedToken_CanAuthenticate()
  {
    var store = new FakeTokenStore();
    var service = CreateService(store);

    var created = await service.CreateAsync("user-123", "Test Device", ["profile:read"]);

    var token = await service.AuthenticateAsync(created.PlainTextToken);

    Assert.NotNull(token);
    Assert.Equal("user-123", token.SubjectId);
  }

  [Fact]
  public async Task InvalidSecret_CannotAuthenticate()
  {
    var store = new FakeTokenStore();
    var service = CreateService(store);

    var created = await service.CreateAsync("user-123", "Test Device");

    var original = created.PlainTextToken;

    var replacement = original[^1] == 'A' ? 'B' : 'A';

    var modified = original[..^1] + replacement;

    var result = await service.AuthenticateAsync(modified);

    Assert.Null(result);
  }

  [Fact]
  public async Task RevokedToken_CannotAuthenticate()
  {
    var store = new FakeTokenStore();
    var service = CreateService(store);

    var created = await service.CreateAsync("user-123", "Test Device");

    var revoked = await service.RevokeAsync(created.Id, "user-123");

    Assert.True(revoked);

    var result = await service.AuthenticateAsync(created.PlainTextToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task ExpiredToken_CannotAuthenticateAtExpiryInstant()
  {
    var clock = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new FakeTokenStore();
    var service = new AuthoraTokenService(
      store,
      clock,
      new AuthoraOptions { TokenLifetime = TimeSpan.FromMinutes(30) }
    );

    var created = await service.CreateAsync("user-123", "Test Device");
    clock.UtcNow = created.ExpiresAt;

    var result = await service.AuthenticateAsync(created.PlainTextToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task CreatedToken_StoresOnlySecretHashAndDistinctAbilities()
  {
    var store = new FakeTokenStore();
    var service = CreateService(store);

    var created = await service.CreateAsync(
      "user-123",
      "Test Device",
      ["profile:read", "profile:read", "profile:write"]
    );
    var persisted = await store.FindByIdAsync(created.Id);

    Assert.NotNull(persisted);
    Assert.DoesNotContain(created.PlainTextToken, persisted.SecretHash, StringComparison.Ordinal);
    Assert.Equal(
      new[] { "profile:read", "profile:write" },
      JsonSerializer.Deserialize<string[]>(persisted.AbilitiesJson)
    );
  }

  [Theory]
  [InlineData("")]
  [InlineData("ara_invalid")]
  [InlineData("other_00000000000000000000000000000000_0000000000000000000000000000000000000000000000000000000000000000")]
  public async Task MalformedToken_CannotAuthenticate(string malformedToken)
  {
    var service = CreateService(new FakeTokenStore());

    var result = await service.AuthenticateAsync(malformedToken);

    Assert.Null(result);
  }

  private sealed class FakeTokenStore : IAuthoraTokenStore
  {
    private readonly Dictionary<Guid, PersonalAccessToken> _tokens = new();

    private readonly HashSet<Guid> _revoked = new();

    public Task CreateAsync(
      PersonalAccessToken token,
      CancellationToken cancellationToken = default
    )
    {
      _tokens.Add(token.Id, token);

      return Task.CompletedTask;
    }

    public Task<PersonalAccessToken?> FindByIdAsync(
      Guid id,
      CancellationToken cancellationToken = default
    )
    {
      PersonalAccessToken? token = null;

      if (!_revoked.Contains(id))
        _tokens.TryGetValue(id, out token);

      return Task.FromResult(token);
    }

    public Task<bool> RevokeAsync(
      Guid tokenId,
      string subjectId,
      DateTimeOffset revokedAt,
      CancellationToken cancellationToken = default
    )
    {
      var exists = _tokens.TryGetValue(tokenId, out var token) && token.SubjectId == subjectId;

      return Task.FromResult(exists && _revoked.Add(tokenId));
    }
  }

}
