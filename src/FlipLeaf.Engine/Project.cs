namespace FlipLeaf
{
    public interface IProject
    {
        IReadOnlyList<ProjectItem> Layouts { get; }

        IReadOnlyList<ProjectItem> Content { get; }

        IReadOnlyList<ProjectItem> Includes { get; }
    }

    public class Project : IProject
    {
        private readonly string _rootDir;
        private readonly List<ProjectItem> _layouts = [];
        private readonly List<ProjectItem> _content = [];
        private readonly List<ProjectItem> _includes = [];

        public Project(string rootDir)
        {
            _rootDir = Path.GetFullPath(rootDir);
        }

        public IReadOnlyList<ProjectItem> Layouts => _layouts;

        public IReadOnlyList<ProjectItem> Content => _content;

        public IReadOnlyList<ProjectItem> Includes => _includes;

        public static string DetectRootDir(string dir)
        {
            if (Directory.Exists(Path.Combine(dir, "content")))
                return dir;

            if (Directory.Exists(Path.Combine(dir, @"..\..\content")))
                return Path.Combine(dir, @"..\..");

            return dir;
        }

        public void Populate()
        {
            foreach (var filePath in Directory.GetFiles(_rootDir, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = filePath.Substring(_rootDir.Length + 1);
                var firstDirName = string.Empty;
                var i = relativePath.IndexOf(Path.DirectorySeparatorChar);
                if (i != -1)
                    firstDirName = relativePath[0..i];

                var item = new ProjectItem(_rootDir, relativePath);

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
    }

    public class ProjectItem
    {
        public ProjectItem(string rootDir, string relativePath)
        {
            Name = Path.GetFileName(relativePath);
            RelativePath = relativePath.Replace('\\', '/');
            FullPath = Path.Combine(rootDir, relativePath).Replace('\\', '/');
            Extension = Path.GetExtension(FullPath);
        }

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

        public override string ToString() => RelativePath;

        public override bool Equals(object? obj) => obj is ProjectItem other && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal);

        public override int GetHashCode() => RelativePath.GetHashCode();

        public string ReadAllText() => File.ReadAllText(FullPath);

        public Stream OpenRead() => new FileStream(FullPath, FileMode.Open, FileAccess.Read);
    }
}
