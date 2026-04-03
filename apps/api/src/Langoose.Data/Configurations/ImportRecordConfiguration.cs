using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ImportRecordConfiguration : IEntityTypeConfiguration<ImportRecord>
{
    public void Configure(EntityTypeBuilder<ImportRecord> builder)
    {
        builder.ToTable("import_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.FileName).HasMaxLength(260);
        builder.HasIndex(record => record.UserId);
    }
}