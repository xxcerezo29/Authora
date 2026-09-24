using Authora.Core.Entities;
using Authora.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Authora.SampleApi.Data;

public sealed class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options)
    : base(options) { }

  public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();
  public DbSet<AuthoraJwtSession> JwtSessions => Set<AuthoraJwtSession>();
  public DbSet<AuthoraRefreshToken> RefreshTokens => Set<AuthoraRefreshToken>();
  public DbSet<AuthoraBrowserSession> BrowserSessions => Set<AuthoraBrowserSession>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new PersonalAccessTokenConfiguration());
    modelBuilder.ApplyConfiguration(new AuthoraJwtSessionConfiguration());
    modelBuilder.ApplyConfiguration(new AuthoraRefreshTokenConfiguration());
    modelBuilder.ApplyConfiguration(new AuthoraBrowserSessionConfiguration());
  }
}
