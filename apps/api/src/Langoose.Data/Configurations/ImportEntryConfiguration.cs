using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ImportEntryConfiguration : IEntityTypeConfiguration<ImportEntry>
{
    public void Configure(EntityTypeBuilder<ImportEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SourceRefId).HasMaxLength(200);
        builder.Property(x => x.Language).HasMaxLength(10);
        builder.Property(x => x.Text).HasMaxLength(300);
        builder.Property(x => x.PartOfSpeech).HasMaxLength(50);
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.StatusReason).HasMaxLength(500);
        builder.Property(x => x.AiReasoning).HasMaxLength(2000);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Source, x.SourceRefId }).IsUnique();
    }
}
