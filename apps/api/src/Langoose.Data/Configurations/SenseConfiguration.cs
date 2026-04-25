using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class SenseConfiguration : IEntityTypeConfiguration<Sense>
{
    public void Configure(EntityTypeBuilder<Sense> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Gloss).HasMaxLength(500);

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany(x => x.Senses)
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DictionaryEntryId, x.SenseIndex }).IsUnique();
    }
}
