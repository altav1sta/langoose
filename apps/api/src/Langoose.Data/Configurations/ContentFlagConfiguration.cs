using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ContentFlagConfiguration : IEntityTypeConfiguration<ContentFlag>
{
    public void Configure(EntityTypeBuilder<ContentFlag> builder)
    {
        builder.ToTable("content_flags");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ItemId);
    }
}
