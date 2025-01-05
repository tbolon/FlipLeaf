namespace FlipLeaf
{
    public class ProjectItem
    {
        public ProjectItem(string rootDir, string relativePath)
        {
            Name = Path.GetFileName(relativePath);
            RelativePath = relativePath.Replace('\\', '/');
            FullPath = Path.Combine(rootDir, relativePath).Replace('\\', '/');
            Extension = Path.GetExtension(FullPath).ToLowerInvariant();
            OutName = Name;
        }

        public Dictionary<string, object?> Properties { get; } = [];

        /// <summary>
        /// Name of the file, including its extension.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Relative path of the file.
        /// </summary>
        public string RelativePath { get; init; }

        public string FullPath { get; init; }

        public string Extension { get; init; }

        public string OutName { get; set; }

        public override string ToString() => RelativePath;

        public override bool Equals(object? obj) => obj is ProjectItem other && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal);

        public override int GetHashCode() => RelativePath.GetHashCode();

        public string ReadAllText() => File.ReadAllText(FullPath);

        public Stream OpenRead() => new FileStream(FullPath, FileMode.Open, FileAccess.Read);
    }
}
