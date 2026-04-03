using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ExampleSentenceConfiguration : IEntityTypeConfiguration<ExampleSentence>
{
    public void Configure(EntityTypeBuilder<ExampleSentence> builder)
    {
        builder.ToTable("example_sentences");
        builder.HasKey(sentence => sentence.Id);
        builder.Property(sentence => sentence.Origin).HasConversion<string>();
        builder.HasIndex(sentence => sentence.ItemId);
    }
}