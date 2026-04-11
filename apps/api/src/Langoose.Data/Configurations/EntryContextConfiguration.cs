using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class EntryContextConfiguration : IEntityTypeConfiguration<EntryContext>
{
    public void Configure(EntityTypeBuilder<EntryContext> builder)
    {
        builder.ToTable("entry_contexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Difficulty).HasMaxLength(20);

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany(x => x.Contexts)
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DictionaryEntryId);
    }
}
