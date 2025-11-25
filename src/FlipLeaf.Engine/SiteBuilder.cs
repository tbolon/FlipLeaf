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

    /// <inheritdoc cref="HostApplicationBuilder.Services"/>
    public IServiceCollection Services => HostBuilder.Services;

    /// <inheritdoc cref="HostApplicationBuilder.Configuration"/>
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

        if (settings.Args?.FirstOrDefault() == KnownVerbs.WatchVerb)
        {
            siteBuilder.Services.AddHostedService<WatcherService>();
            siteBuilder.Services.AddSingleton<IHostLifetime, ConsoleLifetime>();
        }

        return siteBuilder;
    }

    /// <summary>
    /// Detects the most relevant content directory based on path topology.
    /// </summary>
    /// <param name="dir">Working directory</param>
    public static string DetectContentRootPath(string dir)
    {
        // by default we look for a "content" directory in 'dir'
        if (Directory.Exists(Path.Combine(dir, KnownFolders.ContentDir)))
            return dir;

        // next we look 2nd parent, because of the default dotnet location in /bin/Debug when running a program
        if (Directory.Exists(Path.Combine(dir, @"..\..\" + KnownFolders.ContentDir)))
            return Path.Combine(dir, @"..\..");

        // finally, we fallback to the working directory 
        return dir;
    }

    /// <summary>
    /// Builds the site. This method can only be called once.
    /// </summary>
    /// <returns>An initialized <see cref="Site"/>.</returns>
    public Site Build()
    {
        // register "Site" & "ISite"
        Services
            .AddSingleton(sp =>
            {
                var siteOptions = sp.GetRequiredService<IOptions<SiteOptions>>().Value;
                var host = sp.GetRequiredService<IHost>();
                return new Site(host, siteOptions);
            })
            .AddSingleton<ISite>(sp => sp.GetRequiredService<Site>());

        // builds the underlying host
        var host = HostBuilder.Build();

        // returns the "Site" instance
        return host.Services.GetRequiredService<Site>();
    }
}

public static class SiteBuilderExtensions
{
    public static SiteBuilder AddYaml(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<IYamlMarkup, YamlMarkup>();
        return @this;
    }

    public static SiteBuilder AddLiquid(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<ILiquidMarkup, LiquidMarkup>();
        @this.Services.AddSingleton(l => (IWarmup)l.GetRequiredService<ILiquidMarkup>());
        return @this;
    }

    public static SiteBuilder AddMarkdown(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<IMarkdownMarkup, MarkdownMarkup>();
        return @this;
    }
}
