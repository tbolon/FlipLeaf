using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlipLeaf;

public sealed class Builder
{
    public static void Run()
    {
        Console.WriteLine("Hello World!");
    }
}

public sealed class SiteBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;

    public SiteBuilder(HostApplicationBuilderSettings? settings = null)
    {
        _hostBuilder = Host.CreateApplicationBuilder(settings);
    }

    public SiteBuilder(string[]? args = null)
    {
        _hostBuilder = Host.CreateApplicationBuilder(args);
    }

    public SiteBuilder()
    {
        _hostBuilder = Host.CreateApplicationBuilder();
    }

    public Site Build()
    {
        return new Site(_hostBuilder.Build());
    }
}

public sealed class Site : IHost
{
    private readonly IHost _host;

    public Site(IHost host)
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public void Dispose() => _host.Dispose();

    public void Run() => _host.Run();

    public Task RunAsync(CancellationToken cancellationToken = default) => _host.RunAsync(cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken = default) => _host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _host.StopAsync(cancellationToken);
}

