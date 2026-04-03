using Langoose.Auth.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Auth.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityUserContext<AuthUser, Guid>(options)
{
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("auth_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("auth_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("auth_user_tokens");

        builder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
