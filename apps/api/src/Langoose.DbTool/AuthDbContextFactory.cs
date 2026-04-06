using Langoose.Auth.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Langoose.DbTool;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var host = Program.BuildHost(args);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AuthDbContext>>();

        return contextFactory.CreateDbContext();
    }
}
