using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class UserProgressConfiguration : IEntityTypeConfiguration<UserProgress>
{
    public void Configure(EntityTypeBuilder<UserProgress> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.DictionaryEntry)
            .WithMany()
            .HasForeignKey(x => x.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.DictionaryEntryId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.DueAtUtc });
    }
}
