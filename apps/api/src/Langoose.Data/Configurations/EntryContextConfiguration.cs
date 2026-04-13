using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class EntryContextConfiguration : IEntityTypeConfiguration<EntryContext>
{
    public void Configure(EntityTypeBuilder<EntryContext> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Difficulty).HasMaxLength(20);

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany(x => x.Contexts)
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Translations)
            .WithMany()
            .UsingEntity("entry_contexts_translations",
                x => x.HasOne(typeof(EntryContext)).WithMany().HasForeignKey("target_id"),
                x => x.HasOne(typeof(EntryContext)).WithMany().HasForeignKey("source_id"));

        builder.HasIndex(x => x.DictionaryEntryId);
    }
}
