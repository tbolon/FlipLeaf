namespace FlipLeaf;

public interface ILeafAction
{
    Task Execute(LeafContext context);
}

public sealed class Nope : ILeafAction
{
    public static readonly Nope Instance = new();

    private Nope() { }

    public Task Execute(LeafContext content) => Task.CompletedTask;
}

public sealed class CopyLeaf : ILeafAction
{
    private readonly string _fileName;

    public CopyLeaf(string fileName)
    {
        _fileName = fileName;
    }

    public Task Execute(LeafContext context)
    {
        var src = context.Input.FullPath;
        var lastW = File.GetLastWriteTime(src);

        var dest = Path.Combine(context.Site.OutDir, _fileName);
        if (File.Exists(dest) && lastW <= File.GetLastWriteTime(dest))
        {
            // skip if the file is up-to-date
            return Task.CompletedTask;
        }

        var dir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(dir);

        File.Copy(context.Input.FullPath, dest, true);

        context.Output = new LeafOutput { Name = _fileName };

        return Task.CompletedTask;
    }
}

public sealed class ContentFlip : ILeafAction
{
    private readonly string _fileName;
    private readonly string _content;

    public ContentFlip(string fileName, string content)
    {
        _fileName = fileName;
        _content = content;
    }

    public async Task Execute(LeafContext context)
    {
        var dest = Path.Combine(context.Site.OutDir, _fileName);

        var dir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(dir);

        using (var writer = new StreamWriter(dest))
        {
            await writer.WriteAsync(_content);
        }

        context.Output = new LeafOutput { Name = _fileName };
    }
}