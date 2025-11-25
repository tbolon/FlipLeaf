using System.Collections;

namespace FlipLeaf;

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