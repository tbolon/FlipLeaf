namespace FlipLeaf;

/// <summary>
/// Represents the output of a page.
/// </summary>
public class LeafOutput
{
    private readonly string _outDir;
    private readonly string _directory;
    private string _name;

    public LeafOutput(Site site, Leaf input)
    {
        _outDir = site.OutDir;
        _directory = input.RelativeDir;
        _name = input.Name;
    }

    /// <summary>
    /// Name of the file, including its extension.
    /// </summary>
    public string Name
    {
        get => _name;
        set => _name = value;
    }

    /// <summary>
    /// Path of the target directory containing the file.
    /// </summary>
    public string Directory
    {
        get => _directory;
    }

    public string FullPath => Path.Combine(_outDir, _directory, _name);

    public DateTime LastWriteTime => Exists() ? File.GetLastWriteTime(FullPath) : DateTime.MinValue;

    /// <summary>
    /// Gets a value indicating that something has been completed to the output.
    /// The leaf should be considered as transformed and the pipeline terminated.
    /// </summary>
    public LeafOutputStatus Status { get; set; }

    public LeafOutput WithExtension(string extension)
    {
        if (extension.StartsWith('.'))
            Name = Path.GetFileNameWithoutExtension(Name) + extension;
        else
            Name = Path.GetFileNameWithoutExtension(Name) + "." + extension;
        return this;
    }

    public bool Exists() => File.Exists(FullPath);

    public void EnsureDirectory()
    {
        var dir = Path.Combine(_outDir, _directory);
        System.IO.Directory.CreateDirectory(dir);
    }
}

public enum LeafOutputStatus
{
    /// <summary>
    /// The input has been ignored, no output has been generated.
    /// </summary>
    Unhandled,

    /// <summary>
    /// An output has been written.
    /// </summary>
    Written,

    /// <summary>
    /// The output is already up-to-date.
    /// </summary>
    NotChanged,
}