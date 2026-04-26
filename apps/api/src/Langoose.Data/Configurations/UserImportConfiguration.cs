using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class ImportRecordConfiguration : IEntityTypeConfiguration<UserImport>
{
    public void Configure(EntityTypeBuilder<UserImport> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId);
    }
}
