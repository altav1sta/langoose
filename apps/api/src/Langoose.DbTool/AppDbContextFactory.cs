using Langoose.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Langoose.DbTool;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var host = Program.BuildHost(
            args,
            configureAppDatabase: true,
            configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        return contextFactory.CreateDbContext();
    }
}
