using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ReviewStateConfiguration : IEntityTypeConfiguration<ReviewState>
{
    public void Configure(EntityTypeBuilder<ReviewState> builder)
    {
        builder.ToTable("review_states");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ItemId);
    }
}
