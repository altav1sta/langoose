using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class StudyEventConfiguration : IEntityTypeConfiguration<StudyEvent>
{
    public void Configure(EntityTypeBuilder<StudyEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Verdict).HasConversion<string>();
        builder.Property(x => x.FeedbackCode).HasConversion<string>();

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany()
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EntryContext)
            .WithMany()
            .HasForeignKey(x => x.EntryContextId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.DictionaryEntryId);
    }
}
