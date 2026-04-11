using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class EntryTranslationConfiguration : IEntityTypeConfiguration<EntryTranslation>
{
    public void Configure(EntityTypeBuilder<EntryTranslation> builder)
    {
        builder.ToTable("entry_translations");
        builder.HasKey(x => new { x.SourceEntryId, x.TargetEntryId });

        builder.HasOne(x => x.SourceEntry)
            .WithMany(x => x.SourceTranslations)
            .HasForeignKey(x => x.SourceEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetEntry)
            .WithMany(x => x.TargetTranslations)
            .HasForeignKey(x => x.TargetEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
