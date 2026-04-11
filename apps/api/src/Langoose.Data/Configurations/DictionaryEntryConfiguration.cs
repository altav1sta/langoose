using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class DictionaryEntryConfiguration : IEntityTypeConfiguration<DictionaryEntry>
{
    public void Configure(EntityTypeBuilder<DictionaryEntry> builder)
    {
        builder.ToTable("dictionary_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Language).HasMaxLength(10);
        builder.Property(x => x.Text).HasMaxLength(300);
        builder.Property(x => x.GrammarLabel).HasMaxLength(100);
        builder.Property(x => x.Difficulty).HasMaxLength(20);

        builder.HasOne(x => x.BaseEntry)
            .WithMany(x => x.DerivedForms)
            .HasForeignKey(x => x.BaseEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Language, x.Text });
        builder.HasIndex(x => x.BaseEntryId);
    }
}
