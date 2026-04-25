using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class SenseTranslationConfiguration : IEntityTypeConfiguration<SenseTranslation>
{
    public void Configure(EntityTypeBuilder<SenseTranslation> builder)
    {
        builder.HasKey(x => new { x.SourceSenseId, x.TargetSenseId });

        builder.HasOne(x => x.SourceSense)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.SourceSenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetSense)
            .WithMany()
            .HasForeignKey(x => x.TargetSenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TargetSenseId);
    }
}
