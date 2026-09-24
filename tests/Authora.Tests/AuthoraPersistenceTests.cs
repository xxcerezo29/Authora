using Authora.Core.Abstractions;
using Authora.Core.Entities;
using Authora.EntityFrameworkCore.Configurations;
using Authora.EntityFrameworkCore.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Authora.Tests;

public sealed class AuthoraPersistenceTests
{
  [Fact]
  public async Task SqliteStores_PersistTokensSessionsAndSingleUseRefreshRotation()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var clock = new TestSupport.TestTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    var options = new DbContextOptionsBuilder<AuthoraTestDbContext>().UseSqlite(connection).Options;
    await using var db = new AuthoraTestDbContext(options);
    await db.Database.EnsureCreatedAsync();

    var tokenStore = new EfAuthoraTokenStore<AuthoraTestDbContext>(db);
    var token = PersonalAccessToken.Create(Guid.NewGuid(), "user-1", "laptop", new string('A', 64), "[]", clock.GetUtcNow(), clock.GetUtcNow().AddHours(1));
    await tokenStore.CreateAsync(token);
    Assert.Equal("user-1", (await tokenStore.FindByIdAsync(token.Id))?.SubjectId);
    Assert.False(await tokenStore.RevokeAsync(token.Id, "another-user", clock.GetUtcNow()));
    Assert.True(await tokenStore.RevokeAsync(token.Id, "user-1", clock.GetUtcNow()));
    Assert.Equal(clock.GetUtcNow(), (await tokenStore.FindByIdAsync(token.Id))?.RevokedAt);

    var browserStore = new EfAuthoraBrowserSessionStore<AuthoraTestDbContext>(db, clock);
    var sessionKey = await browserStore.CreateAsync("user-1", clock.GetUtcNow(), clock.GetUtcNow().AddHours(2));
    var browserSession = await browserStore.FindAsync(sessionKey);
    Assert.Equal("user-1", browserSession?.SubjectId);
    Assert.DoesNotContain(sessionKey, browserSession!.IdHash, StringComparison.Ordinal);
    await browserStore.RevokeAsync(sessionKey);
    Assert.Null(await browserStore.FindAsync(sessionKey));

    var expiringKey = await browserStore.CreateAsync("user-1", clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1));
    clock.UtcNow = clock.GetUtcNow().AddMinutes(1);
    Assert.Null(await browserStore.FindAsync(expiringKey));

    var refreshStore = new EfAuthoraRefreshStore<AuthoraTestDbContext>(db);
    var jwtSession = AuthoraJwtSession.Create(Guid.NewGuid(), "user-1", clock.GetUtcNow(), clock.GetUtcNow().AddDays(1));
    var current = AuthoraRefreshToken.Create(Guid.NewGuid(), jwtSession.Id, new string('B', 64), clock.GetUtcNow(), clock.GetUtcNow().AddHours(1));
    var replacement = AuthoraRefreshToken.Create(Guid.NewGuid(), jwtSession.Id, new string('C', 64), clock.GetUtcNow().AddMinutes(1), clock.GetUtcNow().AddHours(1));
    await refreshStore.CreateAsync(jwtSession, current);

    Assert.True(await refreshStore.TryRotateAsync(current.Id, replacement, clock.GetUtcNow().AddMinutes(1)));
    Assert.False(await refreshStore.TryRotateAsync(current.Id, replacement, clock.GetUtcNow().AddMinutes(2)));
    Assert.Equal(clock.GetUtcNow().AddMinutes(1), (await refreshStore.FindTokenAsync(current.Id))?.ConsumedAt);
    Assert.Equal(replacement.Id, (await refreshStore.FindTokenAsync(replacement.Id))?.Id);
    Assert.True(await refreshStore.RevokeSessionAsync(jwtSession.Id, "user-1", clock.GetUtcNow().AddMinutes(3)));
    Assert.False(await refreshStore.RevokeSessionAsync(jwtSession.Id, "another-user", clock.GetUtcNow().AddMinutes(4)));
    Assert.Equal(TimeSpan.Zero, (await refreshStore.FindSessionAsync(jwtSession.Id))?.CreatedAt.Offset);
  }

  private sealed class AuthoraTestDbContext(DbContextOptions<AuthoraTestDbContext> options) : DbContext(options)
  {
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.ApplyConfiguration(new PersonalAccessTokenConfiguration());
      modelBuilder.ApplyConfiguration(new AuthoraBrowserSessionConfiguration());
      modelBuilder.ApplyConfiguration(new AuthoraJwtSessionConfiguration());
      modelBuilder.ApplyConfiguration(new AuthoraRefreshTokenConfiguration());
    }
  }
}
