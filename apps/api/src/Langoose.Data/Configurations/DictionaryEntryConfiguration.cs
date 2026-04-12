using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class DictionaryEntryConfiguration : IEntityTypeConfiguration<DictionaryEntry>
{
    public void Configure(EntityTypeBuilder<DictionaryEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Language).HasMaxLength(10);
        builder.Property(x => x.Text).HasMaxLength(300);
        builder.Property(x => x.PartOfSpeech).HasMaxLength(50);
        builder.Property(x => x.GrammarLabel).HasMaxLength(100);
        builder.Property(x => x.Difficulty).HasMaxLength(20);
        builder.HasOne(x => x.BaseEntry)
            .WithMany(x => x.DerivedForms)
            .HasForeignKey(x => x.BaseEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Translations).WithMany();

        builder.HasIndex(x => new { x.Language, x.Text });
        builder.HasIndex(x => new { x.Language, x.Text })
            .HasFilter("base_entry_id IS NULL")
            .IsUnique();
        builder.HasIndex(x => x.BaseEntryId);
    }
}
