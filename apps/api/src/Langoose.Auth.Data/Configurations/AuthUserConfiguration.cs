using Langoose.Auth.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Auth.Data.Configurations;

public sealed class AuthUserConfiguration : IEntityTypeConfiguration<AuthUser>
{
    public void Configure(EntityTypeBuilder<AuthUser> builder)
    {
        builder.ToTable("auth_users");
        builder.Property(x => x.DisplayName)
            .HasMaxLength(200);
        builder.Property(x => x.Provider)
            .HasMaxLength(100);
        builder.Property(x => x.ProviderUserId)
            .HasMaxLength(200);
    }
}
