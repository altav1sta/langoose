using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(320);
        builder.Property(user => user.Name).HasMaxLength(200);
        builder.Property(user => user.Provider).HasMaxLength(100);
        builder.Property(user => user.ProviderUserId).HasMaxLength(200);
    }
}