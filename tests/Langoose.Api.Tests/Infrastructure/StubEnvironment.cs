using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Langoose.Api.Tests.Infrastructure;

internal sealed class StubEnvironment : IWebHostEnvironment
{
    public StubEnvironment(string root)
    {
        ApplicationName = "Langoose.Api.Tests";
        WebRootPath = root;
        WebRootFileProvider = new NullFileProvider();
        EnvironmentName = "Development";
        ContentRootPath = root;
        ContentRootFileProvider = new NullFileProvider();
    }

    public string ApplicationName { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
