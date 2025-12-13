namespace FlipLeaf;

public class Leaf
{
    public Leaf(string containerDir, string relativePath)
    {
        Name = Path.GetFileName(relativePath);
        RelativePath = relativePath.Replace('\\', '/');
        RelativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
        FullPath = Path.Combine(containerDir, relativePath).Replace('\\', '/');
        Extension = Path.GetExtension(FullPath).ToLowerInvariant();
        OutName = Name;
    }

    public Dictionary<string, object?> Properties { get; } = [];

    /// <summary>
    /// Name of the file, including its extension.
    /// E.g.: why-42.md
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Relative path of the file, in an URL format ('/').
    /// E.g.: articles/why-42.md
    /// </summary>
    public string RelativePath { get; init; }

    /// <summary>
    /// Path of the directory containing the file relative to the container directory.
    /// E.g.: articles
    /// </summary>
    public string RelativeDir { get; init; }

    /// <summary>
    /// Full path of the file.
    /// E.g.: d:/repo/content/articles/why-42.md
    /// </summary>
    public string FullPath { get; init; }

    /// <summary>
    /// Extension of the file, including the dot.
    /// E.g.: .md
    /// </summary>
    public string Extension { get; init; }

    public bool Exists => File.Exists(FullPath);

    public DateTime LastWriteTime => Exists ? File.GetLastWriteTime(FullPath) : DateTime.MinValue;

    public string OutName { get; set; }

    public override string ToString() => RelativePath;

    public override bool Equals(object? obj) => obj is Leaf other && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal);

    public override int GetHashCode() => RelativePath.GetHashCode();

    public string ReadAllText() => File.ReadAllText(FullPath);

    public Stream OpenRead() => new FileStream(FullPath, FileMode.Open, FileAccess.Read);

    public ContentFlip AsContentResult(string content, string? outName = null) => new(outName ?? OutName, content);

    public CopyLeaf AsCopyResult() => new(Name);
}
