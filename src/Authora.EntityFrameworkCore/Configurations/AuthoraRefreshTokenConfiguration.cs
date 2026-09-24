using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps refresh token records and their owning JWT session relationship.
/// </summary>
public sealed class AuthoraRefreshTokenConfiguration : IEntityTypeConfiguration<AuthoraRefreshToken>
{
  /// <summary>
  /// Configures the entity mapping.
  /// </summary>
  public void Configure(EntityTypeBuilder<AuthoraRefreshToken> builder)
  {
    builder.ToTable("authora_refresh_tokens");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id).ValueGeneratedNever();

    builder.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();

    builder.Property(x => x.CreatedAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.ExpiresAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.ConsumedAt).HasConversion(AuthoraUtcDateConverter.Instance);

    builder
      .HasOne<AuthoraJwtSession>()
      .WithMany()
      .HasForeignKey(x => x.SessionId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(x => x.SessionId);
  }
}
