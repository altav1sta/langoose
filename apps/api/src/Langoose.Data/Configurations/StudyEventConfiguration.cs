using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class StudyEventConfiguration : IEntityTypeConfiguration<StudyEvent>
{
    public void Configure(EntityTypeBuilder<StudyEvent> builder)
    {
        builder.ToTable("study_events");
        builder.HasKey(studyEvent => studyEvent.Id);
        builder.Property(studyEvent => studyEvent.Verdict).HasConversion<string>();
        builder.Property(studyEvent => studyEvent.FeedbackCode).HasConversion<string>();
        builder.HasIndex(studyEvent => studyEvent.UserId);
        builder.HasIndex(studyEvent => studyEvent.ItemId);
    }
}