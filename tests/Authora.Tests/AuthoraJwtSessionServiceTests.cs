using System.Security.Claims;
using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.Core.Options;
using Authora.Jwt.Options;
using Authora.Jwt.Services;
using Authora.EntityFrameworkCore.Configurations;
using Authora.EntityFrameworkCore.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Authora.Jwt.Authentication;

namespace Authora.Tests;

public sealed class AuthoraJwtSessionServiceTests
{
  [Fact]
  public async Task SignInAndRefresh_RotatesRefreshToken()
  {
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new InMemoryRefreshStore();
    var subjects = new TestSubjectResolver();
    var jwt = new AuthoraJwtService(
      new AuthoraJwtOptions
      {
        Issuer = "authora-tests",
        Audience = "authora-tests",
        SigningKeyBase64 = Convert.ToBase64String(new byte[64]),
      },
      subjects,
      clock
    );
    var service = new AuthoraJwtSessionService(
      store,
      jwt,
      new AuthoraJwtSessionOptions
      {
        SessionLifetime = TimeSpan.FromDays(2),
        RefreshTokenLifetime = TimeSpan.FromHours(6),
      },
      clock
    );

    var initial = await service.SignInAsync("user-1");
    var decoded = new JwtSecurityTokenHandler().ReadJwtToken(initial.AccessToken);
    var replacement = await service.RefreshAsync(initial.RefreshToken);
    var replay = await service.RefreshAsync(initial.RefreshToken);

    Assert.NotEqual(initial.RefreshToken, replacement?.RefreshToken);
    Assert.NotEqual(initial.AccessToken, replacement?.AccessToken);
    Assert.Null(replay);
    Assert.Equal(initial.SessionId, replacement?.SessionId);
    Assert.Contains(decoded.Claims, claim => claim.Type == AuthoraJwtDefaults.SessionIdClaim && claim.Value == initial.SessionId.ToString("N"));
  }

  [Fact]
  public async Task ExpiredRefreshToken_CannotRotate()
  {
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new InMemoryRefreshStore();
    var jwt = new AuthoraJwtService(
      new AuthoraJwtOptions { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(new byte[64]) },
      new TestSubjectResolver(),
      clock
    );
    var service = new AuthoraJwtSessionService(store, jwt, new AuthoraJwtSessionOptions { RefreshTokenLifetime = TimeSpan.FromMinutes(1) }, clock);
    var signedIn = await service.SignInAsync("user-1");
    clock.UtcNow = signedIn.RefreshTokenExpiresAt;

    Assert.Null(await service.RefreshAsync(signedIn.RefreshToken));
  }

  [Fact]
  public async Task MalformedOrWrongSecretRefreshToken_CannotRotate()
  {
    var (service, _) = CreateInMemoryService();
    var signedIn = await service.SignInAsync("user-1");
    var replacementCharacter = signedIn.RefreshToken[^1] == 'A' ? 'B' : 'A';
    var wrongSecret = signedIn.RefreshToken[..^1] + replacementCharacter;

    Assert.Null(await service.RefreshAsync("not-a-refresh-token"));
    Assert.Null(await service.RefreshAsync(wrongSecret));
    Assert.NotNull(await service.RefreshAsync(signedIn.RefreshToken));
  }

  [Fact]
  public async Task RefreshLifetime_IsCappedBySessionExpiry()
  {
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new InMemoryRefreshStore();
    var jwt = CreateJwtService(clock);
    var service = new AuthoraJwtSessionService(
      store,
      jwt,
      new AuthoraJwtSessionOptions { SessionLifetime = TimeSpan.FromHours(8), RefreshTokenLifetime = TimeSpan.FromDays(7) },
      clock
    );

    var signedIn = await service.SignInAsync("user-1");

    Assert.Equal(clock.GetUtcNow().AddHours(8), signedIn.RefreshTokenExpiresAt);
  }

  [Fact]
  public async Task SessionRevocation_IsSubjectScoped_AndExplicitTokenRevokeEndsSession()
  {
    var (service, _) = CreateInMemoryService();
    var signedIn = await service.SignInAsync("user-1");

    Assert.False(await service.RevokeSessionAsync(signedIn.SessionId, "another-user"));
    Assert.True(await service.RevokeSessionAsync(signedIn.SessionId, "user-1"));
    Assert.Null(await service.RefreshAsync(signedIn.RefreshToken));

    var second = await service.SignInAsync("user-1");
    Assert.True(await service.RevokeAsync(second.RefreshToken));
    Assert.Null(await service.RefreshAsync(second.RefreshToken));
  }

  [Fact]
  public async Task ReplayedConsumedRefreshToken_RevokesPersistedSession()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<JwtTestDbContext>().UseSqlite(connection).Options;
    await using var db = new JwtTestDbContext(options);
    await db.Database.EnsureCreatedAsync();
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new EfAuthoraRefreshStore<JwtTestDbContext>(db);
    var jwt = new AuthoraJwtService(
      new AuthoraJwtOptions { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(new byte[64]) },
      new TestSubjectResolver(),
      clock
    );
    var service = new AuthoraJwtSessionService(store, jwt, new AuthoraJwtSessionOptions(), clock);
    var initial = await service.SignInAsync("user-1");

    Assert.NotNull(await service.RefreshAsync(initial.RefreshToken));
    Assert.Null(await service.RefreshAsync(initial.RefreshToken));
    Assert.NotNull((await store.FindSessionAsync(initial.SessionId))?.RevokedAt);
  }

  [Fact]
  public async Task ConcurrentEfRefreshRotations_OnlyOneReplacementIsStored()
  {
    var connectionString = $"Data Source=authora-refresh-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    await using var keeper = new SqliteConnection(connectionString);
    await keeper.OpenAsync();
    var options = new DbContextOptionsBuilder<JwtTestDbContext>().UseSqlite(connectionString).Options;
    var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var session = AuthoraJwtSession.Create(Guid.NewGuid(), "user-1", now, now.AddHours(1));
    var current = AuthoraRefreshToken.Create(Guid.NewGuid(), session.Id, new string('D', 64), now, now.AddMinutes(30));
    await using (var setup = new JwtTestDbContext(options))
    {
      await setup.Database.EnsureCreatedAsync();
      await new EfAuthoraRefreshStore<JwtTestDbContext>(setup).CreateAsync(session, current);
    }

    async Task<bool> RotateAsync(char hashCharacter)
    {
      await using var context = new JwtTestDbContext(options);
      var replacement = AuthoraRefreshToken.Create(Guid.NewGuid(), session.Id, new string(hashCharacter, 64), now, now.AddMinutes(30));
      return await new EfAuthoraRefreshStore<JwtTestDbContext>(context).TryRotateAsync(current.Id, replacement, now.AddMinutes(1));
    }

    var rotations = await Task.WhenAll(RotateAsync('E'), RotateAsync('F'));

    Assert.Single(rotations, rotated => rotated);
    await using var verify = new JwtTestDbContext(options);
    Assert.Equal(2, await verify.Set<AuthoraRefreshToken>().CountAsync());
  }

  private sealed class TestSubjectResolver : IAuthoraSubjectResolver
  {
    public Task<AuthoraSubject?> ResolveAsync(string subjectId, CancellationToken cancellationToken = default) =>
      Task.FromResult<AuthoraSubject?>(subjectId == "user-1" ? new AuthoraSubject(subjectId, [new Claim(ClaimTypes.Name, "User One")]) : null);
  }

  private static (AuthoraJwtSessionService Service, InMemoryRefreshStore Store) CreateInMemoryService()
  {
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store = new InMemoryRefreshStore();
    var service = new AuthoraJwtSessionService(store, CreateJwtService(clock), new AuthoraJwtSessionOptions(), clock);
    return (service, store);
  }

  private static AuthoraJwtService CreateJwtService(TimeProvider clock) => new(
    new AuthoraJwtOptions { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(new byte[64]) },
    new TestSubjectResolver(),
    clock
  );

  private sealed class InMemoryRefreshStore : IAuthoraRefreshStore
  {
    private readonly Dictionary<Guid, AuthoraJwtSession> _sessions = new();
    private readonly Dictionary<Guid, AuthoraRefreshToken> _tokens = new();
    private readonly HashSet<Guid> _consumed = new();
    private readonly HashSet<Guid> _revoked = new();

    public Task CreateAsync(AuthoraJwtSession session, AuthoraRefreshToken token, CancellationToken cancellationToken = default)
    {
      _sessions.Add(session.Id, session);
      _tokens.Add(token.Id, token);
      return Task.CompletedTask;
    }

    public Task<AuthoraRefreshToken?> FindTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
      _tokens.TryGetValue(tokenId, out var token);
      return Task.FromResult(token);
    }

    public Task<AuthoraJwtSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
      _sessions.TryGetValue(sessionId, out var session);
      return Task.FromResult(session is not null && !_revoked.Contains(sessionId) ? session : null);
    }

    public Task<bool> TryRotateAsync(Guid currentTokenId, AuthoraRefreshToken replacement, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
      if (!_tokens.ContainsKey(currentTokenId) || !_consumed.Add(currentTokenId)) return Task.FromResult(false);
      _tokens.Add(replacement.Id, replacement);
      return Task.FromResult(true);
    }

    public Task<bool> RevokeSessionAsync(Guid sessionId, string subjectId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
      var exists = _sessions.TryGetValue(sessionId, out var session) && session.SubjectId == subjectId;
      return Task.FromResult(exists && _revoked.Add(sessionId));
    }
  }

  private sealed class JwtTestDbContext(DbContextOptions<JwtTestDbContext> options) : DbContext(options)
  {
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.ApplyConfiguration(new AuthoraJwtSessionConfiguration());
      modelBuilder.ApplyConfiguration(new AuthoraRefreshTokenConfiguration());
    }
  }
}
