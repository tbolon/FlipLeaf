using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

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

public sealed class PipelineStep
{
    public PipelineStep(ProcessDelegate processDelegate)
    {
        Delegate = processDelegate;
    }

    public ProcessDelegate Delegate { get; }
}

public delegate Task ProcessDelegate(LeafContext context);

public sealed class Site : IHost, ISite
{
    private readonly IHost _host;
    private readonly Project _project;
    private readonly string _dest;
    private readonly List<PipelineStep> _pipelines = [];
    private readonly List<Action<Site>> _postProcesses = [];

    public Site(IHost host, SiteOptions options)
    {
        _host = host;
        _project = new Project(options.RootDir ?? Environment.CurrentDirectory);
        _dest = Path.Combine(_project.RootDir, "out");
        if (!Directory.Exists(_dest))
            Directory.CreateDirectory(_dest);
    }

    public IServiceProvider Services => _host.Services;

    public IProject Project => _project;

    public string Dest => _dest;

    public PipelineStep AddToPipeline(Func<Leaf, Task<string?>> task)
    {
        return AddToPipeline(new Func<Leaf, Task<ILeafAction>>(ExecuteLeafToString));

        async Task<ILeafAction> ExecuteLeafToString(Leaf leaf)
        {
            var content = await task(leaf);
            if (content == null) return NoFlip.Instance;
            var outName = Path.GetFileNameWithoutExtension(leaf.Name) + ".html";
            return new ContentFlip(outName, content);
        }
    }

    public PipelineStep AddToPipeline(Func<Leaf, Task<ILeafAction>> task)
    {
        return AddToPipeline(new ProcessDelegate(ProcessContextToFlip));

        async Task ProcessContextToFlip(LeafContext ctx)
        {
            var flip = await task(ctx.Input);
            await flip.Execute(ctx);
        }
    }

    public PipelineStep AddToPipeline(Func<Leaf, ILeafAction> task)
    {
        return AddToPipeline(new ProcessDelegate(ProcessContextToFlip));

        async Task ProcessContextToFlip(LeafContext ctx)
        {
            var flip = task(ctx.Input);
            await flip.Execute(ctx);
        }
    }

    public PipelineStep AddToPipeline(ProcessDelegate task)
    {
        var step = new PipelineStep(task);
        _pipelines.Add(step);
        return step;
    }

    public void AddPostProcess(Action<Site> postProcess)
    {
        _postProcesses.Add(postProcess);
    }

    public void Dispose() => _host.Dispose();

    public void Run() => this.RunAsync().GetAwaiter().GetResult();

    /// <summary>Start the website generation.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // warmup
        foreach (var warmup in Services.GetServices<IWarmup>())
        {
            await warmup.Warmup(this, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        await GenerateAll(cancellationToken);

        await _host.StartAsync(cancellationToken);
    }

    /// <summary>Stop the website.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _host.StopAsync(cancellationToken);
    }

    public async Task GenerateAll(CancellationToken cancellationToken = default)
    {
        foreach (var leaf in Project.Content)
        {
            await RunPipeline(leaf);
        }

        foreach (var postProcess in _postProcesses)
        {
            postProcess(this);
        }
    }

    public async Task RunPipeline(Leaf leaf)
    {
        Console.WriteLine($"💥 {leaf.Name} generating...");
        var generated = false;

        var ctx = new LeafContext(this, leaf);
        foreach (var step in _pipelines)
        {
            var action = step.Delegate;

            await action.Invoke(ctx);

            if (ctx.Output != null)
            {
                generated = true;
                break;
            }
        }

        if (generated) Console.WriteLine($"✅ {leaf.Name} generated");
        else Console.WriteLine($"🆗 {leaf.Name} up-to-date");
    }
}

public sealed class WatcherService : IHostedService
{
    private readonly Site _site;
    private readonly ILiquidMarkup _liquid;
    private readonly IProject _project;
    private MemoryCache? _memCache;
    private FileSystemWatcher? _watcher;

    public WatcherService(Site site, ILiquidMarkup liquid)
    {
        _site = site;
        _project = _site.Project;
        _liquid = liquid;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // wait for changes
        _memCache = new MemoryCache(new MemoryCacheOptions());
        _watcher = new FileSystemWatcher(Path.Combine(_project.RootDir))
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnChanged;
        _watcher.EnableRaisingEvents = true;

        return Task.CompletedTask;
    }

    private void OnChanged(object s, FileSystemEventArgs e)
    {
        if (e.Name == null) return;
        if (e.Name.StartsWith("out")) return;
        if (e.Name.StartsWith('.')) return;
        Console.WriteLine($"📝 {e.Name} changed");

        _memCache?.GetOrCreate(e.Name, CreateCacheEntry);
    }

    private object CreateCacheEntry(ICacheEntry cacheEntry)
    {
        Console.WriteLine($"🧲 {cacheEntry.Key} create cache entry");
        const int timeout = 500; // ms
        var cts = new CancellationTokenSource(timeout);
        cacheEntry.AddExpirationToken(new CancellationChangeToken(cts.Token)); // create
        cacheEntry.SlidingExpiration = TimeSpan.FromMilliseconds(timeout - 100);
        cacheEntry.RegisterPostEvictionCallback(OnCacheEntryExpired, state: cts);
        return cacheEntry.Key;
    }

    private async void OnCacheEntryExpired(object key, object? value, EvictionReason reason, object? state)
    {
        Console.WriteLine($"🌪️ {key} evicted");
        if (value == null || state == null) return;
        var name = (string)value;

        var i = name.IndexOf('\\');
        if (i == -1)
        {
            return; // skip root items
        }

        var folder = name[..i];
        name = name[(i + 1)..];
        switch (folder)
        {
            case "content":
                var item = _project.Content.FirstOrDefault(i => i.Name == name);
                if (item != null)
                {
                    await _site.RunPipeline(item);
                }
                break;

            case "includes":
            case "layouts":
                _liquid.LoadTemplates(_project);
                await _site.GenerateAll();
                break;
        }

        ((CancellationTokenSource?)state)?.Dispose();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _memCache?.Dispose();
        return Task.CompletedTask;
    }
}
