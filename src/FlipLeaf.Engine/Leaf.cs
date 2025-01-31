namespace FlipLeaf
{
    public class Leaf
    {
        public Leaf(string rootDir, string relativePath)
        {
            Name = Path.GetFileName(relativePath);
            RelativePath = relativePath.Replace('\\', '/');
            FullPath = Path.Combine(rootDir, relativePath).Replace('\\', '/');
            Extension = Path.GetExtension(FullPath).ToLowerInvariant();
            OutName = Name;
        }

        public Dictionary<string, object?> Properties { get; } = [];

        /// <summary>
        /// Name of the file, including its extension, relative to the project root.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Relative path of the file.
        /// </summary>
        public string RelativePath { get; init; }

        /// <summary>
        /// Full path of the file.
        /// </summary>
        public string FullPath { get; init; }

        public string Extension { get; init; }

        public string OutName { get; set; }

        public override string ToString() => RelativePath;

        public override bool Equals(object? obj) => obj is Leaf other && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal);

        public override int GetHashCode() => RelativePath.GetHashCode();

        public string ReadAllText() => File.ReadAllText(FullPath);

        public Stream OpenRead() => new FileStream(FullPath, FileMode.Open, FileAccess.Read);

        public NoFlip FlipToNothing() => NoFlip.Instance;

        public ContentFlip FlipToContent(string content, string? outName = null) => new ContentFlip(outName ?? OutName, content);

        public CopyLeaf FlipToCopy() => new CopyLeaf(Name);
    }
}
