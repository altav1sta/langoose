using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ExampleSentenceConfiguration : IEntityTypeConfiguration<ExampleSentence>
{
    public void Configure(EntityTypeBuilder<ExampleSentence> builder)
    {
        builder.ToTable("example_sentences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Origin).HasConversion<string>();
        builder.HasIndex(x => x.ItemId);
    }
}
