using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace FlipLeaf;

public sealed class SiteBuilder
{
    public SiteBuilder(HostApplicationBuilderSettings? settings = null)
    {
        HostBuilder = new HostApplicationBuilder(settings);
    }

    public IServiceCollection Services => HostBuilder.Services;

    public ConfigurationManager Configuration => HostBuilder.Configuration;

    internal HostApplicationBuilder HostBuilder { get; }

    public static SiteBuilder CreateDefault() => CreateDefault(args: null);

    public static SiteBuilder CreateDefault(string[]? args) => CreateDefault(new HostApplicationBuilderSettings { Args = args });

    public static SiteBuilder CreateDefault(HostApplicationBuilderSettings settings)
    {
        var siteBuilder = new SiteBuilder(settings);

        siteBuilder.Services.AddOptions().Configure<SiteOptions>(options =>
        {
            options.RootDir = DetectContentRootPath(siteBuilder.HostBuilder.Environment.ContentRootPath ?? Environment.CurrentDirectory);
        });

        if (settings.Args?.FirstOrDefault() == "watch")
        {
            siteBuilder.Services.AddHostedService<WatcherService>();
            siteBuilder.Services.AddSingleton<IHostLifetime, ConsoleLifetime>();
        }

        return siteBuilder;
    }

    /// <summary>
    /// Detects the most relevant content directory based on path topology.
    /// </summary>
    public static string DetectContentRootPath(string dir)
    {
        if (Directory.Exists(Path.Combine(dir, "content")))
            return dir;

        if (Directory.Exists(Path.Combine(dir, @"..\..\content")))
            return Path.Combine(dir, @"..\..");

        return dir;
    }

    public Site Build()
    {
        Services.AddSingleton(sp =>
        {
            var siteOptions = sp.GetRequiredService<IOptions<SiteOptions>>().Value;
            var host = sp.GetRequiredService<IHost>();
            return new Site(host, siteOptions);
        });

        Services.AddSingleton<ISite>(sp => sp.GetRequiredService<Site>());

        var host = HostBuilder.Build();

        return host.Services.GetRequiredService<Site>();
    }
}
