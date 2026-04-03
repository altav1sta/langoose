using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.ToTable("dictionary_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasConversion<string>();
        builder.Property(x => x.ItemKind).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.EnglishText).HasMaxLength(300);
        builder.Property(x => x.PartOfSpeech).HasMaxLength(100);
        builder.Property(x => x.Difficulty).HasMaxLength(20);
        builder.Property(x => x.CreatedByFlow).HasMaxLength(100);
        builder.Property(x => x.RussianGlosses).HasColumnType("text[]");
        builder.Property(x => x.Tags).HasColumnType("text[]");
        builder.Property(x => x.Distractors).HasColumnType("text[]");
        builder.Property(x => x.AcceptedVariants).HasColumnType("text[]");
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.EnglishText);
    }
}
