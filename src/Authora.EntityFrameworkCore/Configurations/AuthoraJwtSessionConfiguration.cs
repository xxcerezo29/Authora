using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps JWT session records and their owner/expiry data.
/// </summary>
public sealed class AuthoraJwtSessionConfiguration : IEntityTypeConfiguration<AuthoraJwtSession>
{
  /// <summary>
  /// Configures the entity mapping.
  /// </summary>
  public void Configure(EntityTypeBuilder<AuthoraJwtSession> builder)
  {
    builder.ToTable("authora_jwt_sessions");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id).ValueGeneratedNever();

    builder.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();

    builder.Property(x => x.CreatedAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.ExpiresAt).HasConversion(AuthoraUtcDateConverter.Instance).IsRequired();

    builder.Property(x => x.RevokedAt).HasConversion(AuthoraUtcDateConverter.Instance);

    builder.HasIndex(x => x.SubjectId);
  }
}
