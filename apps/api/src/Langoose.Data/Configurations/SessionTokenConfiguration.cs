using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Data.Configurations;

public sealed class SessionTokenConfiguration : IEntityTypeConfiguration<SessionToken>
{
    public void Configure(EntityTypeBuilder<SessionToken> builder)
    {
        builder.ToTable("session_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Token).HasMaxLength(200);
        builder.HasIndex(token => token.Token).IsUnique();
        builder.HasIndex(token => token.UserId);
    }
}