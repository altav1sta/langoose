using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ContextTranslationConfiguration : IEntityTypeConfiguration<ContextTranslation>
{
    public void Configure(EntityTypeBuilder<ContextTranslation> builder)
    {
        builder.ToTable("context_translations");
        builder.HasKey(x => new { x.SourceContextId, x.TargetContextId });

        builder.HasOne(x => x.SourceContext)
            .WithMany(x => x.SourceContextTranslations)
            .HasForeignKey(x => x.SourceContextId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetContext)
            .WithMany(x => x.TargetContextTranslations)
            .HasForeignKey(x => x.TargetContextId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
