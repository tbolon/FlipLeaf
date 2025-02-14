using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections;

namespace FlipLeaf;

public interface ISite
{
    /// <summary>
    /// Absolute path to the root directory for the site.
    /// </summary>
    string RootDir { get; }

    /// <summary>
    /// Absolute path to the directory where the output items must be written.
    /// </summary>
    string OutDir { get; }

    IReadOnlyList<Leaf> Layouts { get; }

    LeafDirectory Content { get; }

    IReadOnlyList<Leaf> Includes { get; }

    void Populate();

    IServiceProvider Services { get; }
}

public static class LeafDirectoryExtensions
{
    public static PipelineStep<LeafFileOutput> Add(this LeafDirectory @this, Func<Leaf, Task<string?>> task)
    {
        return @this.Add(new Func<Leaf, Task<ILeafAction<LeafFileOutput>>>(ProcessFlip));

        async Task<ILeafAction<LeafFileOutput>> ProcessFlip(Leaf leaf)
        {
            var content = await task(leaf);
            if (content == null) return Nope.Instance;
            var outName = Path.GetFileNameWithoutExtension(leaf.Name) + ".html";
            return leaf.AsContentResult(content, outName);
        }
    }

    public static PipelineStep<TOut> Add<TOut>(this LeafDirectory @this, Func<ISite, Leaf, TOut, Task<ILeafAction<TOut>>> task)
    {
        return @this.Add(new ProcessDelegate<TOut>(ProcessFlip));

        async Task ProcessFlip(LeafContext<TOut> ctx)
        {
            var flip = await task(@this.Site, ctx.Input, ctx.Output);
            await flip.Execute(ctx);
        }
    }

    public static PipelineStep<TOut> Add<TOut>(this LeafDirectory @this, Func<ISite, Leaf, Task<ILeafAction<TOut>>> task)
    {
        return @this.Add(new ProcessDelegate<TOut>(ProcessFlip));

        async Task ProcessFlip(LeafContext<TOut> ctx)
        {
            var flip = await task(@this.Site, ctx.Input);
            await flip.Execute(ctx);
        }

    }

    public static PipelineStep<TOut> Add<TOut>(this LeafDirectory @this, Func<Leaf, Task<ILeafAction<TOut>>> task)
    {
        return @this.Add(new ProcessDelegate<TOut>(ProcessFlip));

        async Task ProcessFlip(LeafContext<TOut> ctx)
        {
            var flip = await task(ctx.Input);
            await flip.Execute(ctx);
        }
    }

    public static PipelineStep<TOut> Add<TOut>(this LeafDirectory @this, Func<Leaf, ILeafAction<TOut>> task)
    {
        return @this.Add(new ProcessDelegate<TOut>(ProcessFlip));

        async Task ProcessFlip(LeafContext<TOut> ctx)
        {
            var flip = task(ctx.Input);
            await flip.Execute(ctx);
        }
    }
}

public class LeafDirectory : IEnumerable<Leaf>
{
    private readonly Dictionary<Type, PipelineStepCollection> _collections = [];
    private readonly List<Leaf> _leaves = new List<Leaf>();

    public LeafDirectory(ISite site, string path)
    {
        Site = site;
        Path = System.IO.Path.Combine(site.RootDir, path);
        Name = System.IO.Path.GetDirectoryName(Path) ?? throw new ArgumentException(nameof(path), $"Unable to detect directory name from {path}");
    }

    public string Path { get; }

    public string Name { get; }

    internal ISite Site { get; }

    public void Populate()
    {
        _leaves.Clear();
        foreach (var filePath in Directory.GetFiles(Path, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = filePath[(Path.Length + 1)..];
            _leaves.Add(new Leaf(Path, relativePath));
        }
    }

    public async Task Run<TOut>(LeafContext<TOut> ctx)
    {
        if (!_collections.TryGetValue(typeof(TOut), out var collection))
        {
            return;
        }

        var typeCollection = (PipelineStepCollection<TOut>)collection;
        var stoppable = ctx.Output as IStoppableOutput;

        foreach (var step in typeCollection)
        {
            await step.Delegate.Invoke(ctx);

            if (stoppable?.ShouldStop() == true)
                break;
        }
    }

    public PipelineStep<TOut> Add<TOut>(ProcessDelegate<TOut> task)
    {
        var type = typeof(TOut);
        if (!_collections.TryGetValue(type, out var container))
        {
            _collections[type] = container = new PipelineStepCollection<TOut>();
        }

        return ((PipelineStepCollection<TOut>)container).Add(task);
    }

    public IEnumerator<Leaf> GetEnumerator() => _leaves.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public abstract class PipelineStepCollection
{
    private readonly Type _outType;

    protected PipelineStepCollection(Type outType)
    {
        _outType = outType;
    }
}

public sealed class PipelineStepCollection<TOut> : PipelineStepCollection, IEnumerable<PipelineStep<TOut>>
{
    private readonly List<PipelineStep<TOut>> _steps = [];

    public PipelineStepCollection() : base(typeof(TOut))
    {
    }

    public PipelineStep<TOut> Add(ProcessDelegate<TOut> task)
    {
        var step = new PipelineStep<TOut>(task);
        _steps.Add(step);
        return step;
    }

    public IEnumerator<PipelineStep<TOut>> GetEnumerator() => _steps.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Site : IHost, ISite
{
    private readonly IHost _host;
    private readonly LeafDirectory _content;
    private readonly List<Action<Site>> _postProcesses = [];
    private readonly List<Leaf> _layouts = [];
    private readonly List<Leaf> _includes = [];

    public Site(IHost host, SiteOptions options)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ArgumentNullException.ThrowIfNull(options);

        RootDir = Path.GetFullPath(options.RootDir ?? Environment.CurrentDirectory);

        _content = new LeafDirectory(this, options.ContentDir);

        IncludesDir = Path.Combine(RootDir, KnownFolders.IncludesDir);
        LayoutsDir = Path.Combine(RootDir, KnownFolders.LayoutsDir);

        OutDir = Path.Combine(RootDir, KnownFolders.OutDir);
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);
    }

    public LeafDirectory Content => _content;

    public IServiceProvider Services => _host.Services;

    public IReadOnlyList<Leaf> Layouts => _layouts;

    public IReadOnlyList<Leaf> Includes => _includes;

    /// <summary>
    /// Root directory of the site.
    /// </summary>
    public string RootDir { get; }

    /// <summary>
    /// Directory where output files should be written.
    /// </summary>
    public string OutDir { get; }

    private string IncludesDir { get; }

    private string LayoutsDir { get; }

    public void Populate()
    {
        _content.Populate();

        _includes.Clear();
        foreach (var filePath in Directory.GetFiles(IncludesDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = filePath[(IncludesDir.Length + 1)..];
            _includes.Add(new Leaf(IncludesDir, relativePath));
        }

        _layouts.Clear();
        foreach (var filePath in Directory.GetFiles(LayoutsDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = filePath[(LayoutsDir.Length + 1)..];
            _layouts.Add(new Leaf(LayoutsDir, relativePath));
        }
    }

    public void AddPostProcess(Action<Site> postProcess)
    {
        _postProcesses.Add(postProcess);
    }

    public void Dispose() => _host.Dispose();

    public void Run(string[] args) => this.RunAsync(args).GetAwaiter().GetResult();

    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.FirstOrDefault() == KnownVerbs.WatchVerb)
        {
            await this.RunAsync(cancellationToken);
        }
        else
        {
            await ((IHost)this).StartAsync(cancellationToken);
            await ((IHost)this).StopAsync(cancellationToken);
        }
    }

    /// <summary>Start the website generation.</summary>
    async Task IHost.StartAsync(CancellationToken cancellationToken)
    {
        // populate
        this.Populate();

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
    Task IHost.StopAsync(CancellationToken cancellationToken)
    {
        return _host.StopAsync(cancellationToken);
    }

    public async Task GenerateAll(CancellationToken cancellationToken = default)
    {
        foreach (var leaf in _content)
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

        var output = new LeafFileOutput(this, leaf);
        var ctx = new LeafContext<LeafFileOutput>(this, leaf, output);

        await _content.Run(ctx);

        if (ctx.Output.Status == LeafOutputStatus.Written) Console.WriteLine($"✅ {leaf.Name} written");
        else Console.WriteLine($"🆗 {leaf.Name} up-to-date");
    }
}
