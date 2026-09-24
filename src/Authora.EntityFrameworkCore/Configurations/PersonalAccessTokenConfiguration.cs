using Authora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.EntityFrameworkCore.Configurations;

/// <summary>
/// Maps personal access token records and their owner lookup index.
/// </summary>
public sealed class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<PersonalAccessToken>
{
  /// <summary>
  /// Configures the entity mapping.
  /// </summary>
  public void Configure(EntityTypeBuilder<PersonalAccessToken> builder)
  {
    builder.ToTable("authora_personal_access_tokens");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id).ValueGeneratedNever();

    builder.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();

    builder.Property(x => x.Name).HasMaxLength(128).IsRequired();

    builder.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();

    builder.Property(x => x.AbilitiesJson).HasColumnName("Abilities").IsRequired();

    builder.Property(x => x.CreatedAt).IsRequired();

    builder.Property(x => x.ExpiresAt).IsRequired();

    builder.HasIndex(x => x.SubjectId);
  }
}
