using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlipLeaf;

public delegate Task ProcessDelegate(LeafContext context);

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

    IReadOnlyList<Leaf> Content { get; }

    IReadOnlyList<Leaf> Includes { get; }

    void Populate();

    IServiceProvider Services { get; }
}

public sealed class Site : IHost, ISite
{
    private readonly IHost _host;
    private readonly List<PipelineStep> _pipelines = [];
    private readonly List<Action<Site>> _postProcesses = [];
    private readonly List<Leaf> _layouts = [];
    private readonly List<Leaf> _content = [];
    private readonly List<Leaf> _includes = [];

    public Site(IHost host, SiteOptions options)
    {
        _host = host;
        RootDir = Path.GetFullPath(options.RootDir ?? Environment.CurrentDirectory);

        ContentDir = Path.Combine(RootDir, options.ContentDir);
        IncludesDir = Path.Combine(RootDir, KnownFolders.IncludesDir);
        LayoutsDir = Path.Combine(RootDir, KnownFolders.LayoutsDir);

        OutDir = Path.Combine(RootDir, KnownFolders.OutDir);
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);
    }

    public IServiceProvider Services => _host.Services;

    public IReadOnlyList<Leaf> Layouts => _layouts;

    public IReadOnlyList<Leaf> Content => _content;

    public IReadOnlyList<Leaf> Includes => _includes;

    /// <summary>
    /// Root directory of the site.
    /// </summary>
    public string RootDir { get; }

    /// <summary>
    /// Directory where output files should be written.
    /// </summary>
    public string OutDir { get; }

    /// <summary>
    /// Directory from which content files are read.
    /// </summary>
    public string ContentDir { get; }

    private string IncludesDir { get; }

    private string LayoutsDir { get; }

    public void Populate()
    {
        _content.Clear();
        foreach (var filePath in Directory.GetFiles(ContentDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = filePath[(ContentDir.Length + 1)..];
            _content.Add(new Leaf(ContentDir, relativePath));
        }

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

    public PipelineStep AddToPipeline(Func<Leaf, Task<string?>> task)
    {
        return AddToPipeline(new Func<Leaf, Task<ILeafAction>>(ExecuteLeafToString));

        async Task<ILeafAction> ExecuteLeafToString(Leaf leaf)
        {
            var content = await task(leaf);
            if (content == null) return Nope.Instance;
            var outName = Path.GetFileNameWithoutExtension(leaf.Name) + ".html";
            return leaf.AsContentResult(content, outName);
        }
    }

    public PipelineStep AddToPipeline(Func<ISite, Leaf, LeafOutput, Task<ILeafAction>> task)
    {
        return AddToPipeline(new ProcessDelegate(ProcessContextToFlip));

        async Task ProcessContextToFlip(LeafContext ctx)
        {
            var flip = await task(this, ctx.Input, ctx.Output);
            await flip.Execute(ctx);
        }
    }

    public PipelineStep AddToPipeline(Func<ISite, Leaf, Task<ILeafAction>> task)
    {
        return AddToPipeline(new ProcessDelegate(ProcessContextToFlip));

        async Task ProcessContextToFlip(LeafContext ctx)
        {
            var flip = await task(this, ctx.Input);
            await flip.Execute(ctx);
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
        foreach (var leaf in this.Content)
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
        var written = false;

        var ctx = new LeafContext(this, leaf);
        foreach (var step in _pipelines)
        {
            var action = step.Delegate;

            await action.Invoke(ctx);

            if (ctx.Output.Status != LeafOutputStatus.Unhandled)
            {
                leaf.OutName = ctx.Output.Name;
                written = ctx.Output.Status == LeafOutputStatus.Written;
                break;
            }
        }

        if (written) Console.WriteLine($"✅ {leaf.Name} written");
        else Console.WriteLine($"🆗 {leaf.Name} up-to-date");
    }
}
