using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ImportRecordConfiguration : IEntityTypeConfiguration<ImportRecord>
{
    public void Configure(EntityTypeBuilder<ImportRecord> builder)
    {
        builder.ToTable("import_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileHash).HasMaxLength(128);

        builder.HasIndex(x => x.UserId);
    }
}
