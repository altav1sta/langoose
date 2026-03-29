using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ContentFlagConfiguration : IEntityTypeConfiguration<ContentFlag>
{
    public void Configure(EntityTypeBuilder<ContentFlag> builder)
    {
        builder.ToTable("content_flags");
        builder.HasKey(flag => flag.Id);
        builder.HasIndex(flag => flag.UserId);
        builder.HasIndex(flag => flag.ItemId);
    }
}