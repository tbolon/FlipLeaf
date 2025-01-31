namespace FlipLeaf;

public sealed class LeafContext
{
    public LeafContext(Site site, Leaf input)
    {
        Site = site;
        Input = input;
    }

    public Site Site { get; }

    public Leaf Input { get; }

    public LeafOutput? Output { get; set; }
}


/// <summary>
/// Represents the output of a page.
/// </summary>
public sealed class LeafOutput
{
    /// <summary>
    /// Name of the file, including its extension, relative to the output directory.
    /// </summary>
    public string? Name { get; set; }
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
        var fileName = Path.Combine(context.Site.Dest, _fileName);
        var dir = Path.GetDirectoryName(fileName)!;
        Directory.CreateDirectory(dir);

        using (var writer = new StreamWriter(fileName))
        {
            await writer.WriteAsync(_content);
        }

        context.Output = new LeafOutput { Name = _fileName };
    }
}

public interface ILeafAction
{
    Task Execute(LeafContext context);
}

public sealed class NoFlip : ILeafAction
{
    public static NoFlip Instance = new();

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

        var dest = Path.Combine(context.Site.Dest, _fileName);
        if (File.Exists(dest) && lastW <= File.GetLastWriteTime(dest))
        {
            // skip if the file is up-to-date
            return Task.FromResult(false);
        }

        var dir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(dir);

        File.Copy(context.Input.FullPath, dest, true);

        context.Output = new LeafOutput { Name = _fileName };

        return Task.CompletedTask;
    }
}