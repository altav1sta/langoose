using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class UserDictionaryEntryConfiguration : IEntityTypeConfiguration<UserDictionaryEntry>
{
    public void Configure(EntityTypeBuilder<UserDictionaryEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EnrichmentStatus).HasConversion<string>();
        builder.Property(x => x.SourceLanguage).HasMaxLength(10);
        builder.Property(x => x.TargetLanguage).HasMaxLength(10);
        builder.Property(x => x.UserInputTerm).HasMaxLength(300);
        builder.Property(x => x.UserInputTranslation).HasMaxLength(300);
        builder.Property(x => x.Type).HasMaxLength(20);
        builder.Property(x => x.Tags).HasColumnType("text[]");

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany()
            .HasForeignKey(x => x.DictionaryEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.DictionaryEntryId });
        builder.HasIndex(x => new { x.EnrichmentStatus, x.CreatedAtUtc });
    }
}
