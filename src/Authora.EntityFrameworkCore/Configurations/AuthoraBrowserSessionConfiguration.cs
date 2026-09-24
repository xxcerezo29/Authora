using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps hashed browser session records and their expiration fields.
/// </summary>
public sealed class AuthoraBrowserSessionConfiguration
  : IEntityTypeConfiguration<AuthoraBrowserSession>
{
  /// <summary>
  /// Configures the entity mapping.
  /// </summary>
  public void Configure(EntityTypeBuilder<AuthoraBrowserSession> builder)
  {
    builder.ToTable("authora_browser_sessions");

    builder.HasKey(x => x.IdHash);

    builder.Property(x => x.IdHash).HasMaxLength(64).ValueGeneratedNever();

    builder.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();

    builder.Property(x => x.CreatedAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.ExpiresAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.RevokedAt).HasConversion(AuthoraUtcDateConverter.Instance);

    builder.HasIndex(x => x.SubjectId);
  }
}
