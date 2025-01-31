using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlipLeaf;


public delegate Task ProcessDelegate(LeafContext context);

public interface ISite
{
    string RootDir { get; }

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
        OutDir = Path.Combine(RootDir, "out");
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);
    }

    public IServiceProvider Services => _host.Services;

    public IReadOnlyList<Leaf> Layouts => _layouts;

    public IReadOnlyList<Leaf> Content => _content;

    public IReadOnlyList<Leaf> Includes => _includes;

    public string RootDir { get; }

    public string OutDir { get; }

    public void Populate()
    {
        foreach (var filePath in Directory.GetFiles(RootDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = filePath.Substring(RootDir.Length + 1);
            var firstDirName = string.Empty;
            var i = relativePath.IndexOf(Path.DirectorySeparatorChar);
            if (i != -1)
                firstDirName = relativePath[0..i];

            var item = new Leaf(RootDir, relativePath);

            switch (firstDirName.ToLowerInvariant())
            {
                case "layouts":
                    _layouts.Add(item);
                    break;

                case "content":
                    _content.Add(item);
                    break;

                case "includes":
                    _includes.Add(item);
                    break;
            }
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
            return new ContentFlip(outName, content);
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
        if (args.FirstOrDefault() == "watch")
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
        var generated = false;

        var ctx = new LeafContext(this, leaf);
        foreach (var step in _pipelines)
        {
            var action = step.Delegate;

            await action.Invoke(ctx);

            if (ctx.Output?.Name != null)
            {
                leaf.OutName = ctx.Output.Name;
                generated = true;
                break;
            }
        }

        if (generated) Console.WriteLine($"✅ {leaf.Name} generated");
        else Console.WriteLine($"🆗 {leaf.Name} up-to-date");
    }
}
