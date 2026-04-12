using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ContentFlagConfiguration : IEntityTypeConfiguration<ContentFlag>
{
    public void Configure(EntityTypeBuilder<ContentFlag> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany()
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DictionaryEntryId);
    }
}
