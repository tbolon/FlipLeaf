using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace FlipLeaf;

public interface IWarmup
{
    Task Warmup(ISite site, CancellationToken cancellationToken);
}

public interface ISite
{
    IProject Project { get; }
}

public class SiteOptions
{
    /// <summary>
    /// Path where the site content is located.
    /// Defaults to "./content".
    /// </summary>
    public string? RootDir { get; set; }
}


public sealed class Site : IHost, ISite
{
    private readonly IHost _host;
    private readonly Project _project;

    public Site(IHost host, SiteOptions options)
    {
        _host = host;
        _project = new Project(options.RootDir ?? Environment.CurrentDirectory);
    }

    public IServiceProvider Services => _host.Services;

    public IProject Project => _project;

    public void Dispose() => _host.Dispose();

    public void Run() => _host.Run();

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        // warmup
        foreach (var warmup in Services.GetServices<IWarmup>())
        {
            warmup.Warmup(this, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return _host.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Start the website generation.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return _host.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => _host.StopAsync(cancellationToken);
}

