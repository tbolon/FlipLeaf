namespace FlipLeaf;

/// <summary>
/// Describes an action which should be executed against a <see cref="LeafContext{TOut}"/> of <see cref="LeafFileOutput"/> to generate this output.
/// </summary>
public interface ILeafAction<TOut>
{
    /// <summary>
    /// Execute the action.
    /// </summary>
    Task Execute(LeafContext<TOut> context);
}

public sealed class Nope : ILeafAction<LeafFileOutput>
{
    public static readonly Nope Instance = new();

    private Nope() { }

    public Task Execute(LeafContext<LeafFileOutput> content) => Task.CompletedTask;
}

public sealed class CopyLeaf : ILeafAction<LeafFileOutput>
{
    private readonly string _fileName;

    public CopyLeaf(string fileName)
    {
        _fileName = fileName;
    }

    public Task Execute(LeafContext<LeafFileOutput> context)
    {
        var src = context.Input.FullPath;
        context.Output.Name = _fileName;

        var lastW = File.GetLastWriteTime(src);
        var dest = context.Output.FullPath;
        if (File.Exists(dest) && lastW <= File.GetLastWriteTime(dest))
        {
            // skip if the file is up-to-date
            context.Output.Status = LeafOutputStatus.NotChanged;
            return Task.CompletedTask;
        }

        var dir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(dir);

        File.Copy(context.Input.FullPath, dest, true);
        File.SetLastWriteTime(dest, lastW);

        context.Output.Status = LeafOutputStatus.Written;

        return Task.CompletedTask;
    }
}

public sealed class FlipStatus : ILeafAction<LeafFileOutput>
{
    public static readonly FlipStatus Unhandled = new(LeafOutputStatus.Unhandled);
    public static readonly FlipStatus Written= new(LeafOutputStatus.Written);
    public static readonly FlipStatus NotChanged= new(LeafOutputStatus.NotChanged);

    private readonly LeafOutputStatus _status;

    private FlipStatus(LeafOutputStatus status)
    {
        _status = status;
    }

    public Task Execute(LeafContext<LeafFileOutput> context)
    {
        context.Output.Status = _status;
        return Task.CompletedTask;
    }
}

public sealed class ContentFlip : ILeafAction<LeafFileOutput>
{
    private readonly string _fileName;
    private readonly string _content;

    public ContentFlip(string fileName, string content)
    {
        _fileName = fileName;
        _content = content;
    }

    public async Task Execute(LeafContext<LeafFileOutput> context)
    {
        context.Output.Name = _fileName;
        context.Output.EnsureDirectory();
        using var writer = new StreamWriter(context.Output.FullPath);
        await writer.WriteAsync(_content);
        context.Output.Status = LeafOutputStatus.Written;
    }
}