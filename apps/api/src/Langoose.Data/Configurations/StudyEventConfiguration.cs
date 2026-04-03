using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class StudyEventConfiguration : IEntityTypeConfiguration<StudyEvent>
{
    public void Configure(EntityTypeBuilder<StudyEvent> builder)
    {
        builder.ToTable("study_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Verdict).HasConversion<string>();
        builder.Property(x => x.FeedbackCode).HasConversion<string>();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ItemId);
    }
}
