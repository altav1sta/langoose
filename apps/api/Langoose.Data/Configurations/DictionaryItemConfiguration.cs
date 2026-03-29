using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.ToTable("dictionary_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceType).HasConversion<string>();
        builder.Property(item => item.ItemKind).HasConversion<string>();
        builder.Property(item => item.Status).HasConversion<string>();
        builder.Property(item => item.EnglishText).HasMaxLength(300);
        builder.Property(item => item.PartOfSpeech).HasMaxLength(100);
        builder.Property(item => item.Difficulty).HasMaxLength(20);
        builder.Property(item => item.CreatedByFlow).HasMaxLength(100);
        builder.Property(item => item.RussianGlosses).HasColumnType("text[]");
        builder.Property(item => item.Tags).HasColumnType("text[]");
        builder.Property(item => item.Distractors).HasColumnType("text[]");
        builder.Property(item => item.AcceptedVariants).HasColumnType("text[]");
        builder.HasIndex(item => item.OwnerId);
        builder.HasIndex(item => item.EnglishText);
    }
}