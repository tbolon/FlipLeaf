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
        private readonly List<ProjectItem> _layouts = [];
        private readonly List<ProjectItem> _content = [];
        private readonly List<ProjectItem> _includes = [];

        public Project(string rootDir)
        {
            RootDir = Path.GetFullPath(rootDir);
        }

        public string RootDir { get; }

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
            foreach (var filePath in Directory.GetFiles(RootDir, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = filePath.Substring(RootDir.Length + 1);
                var firstDirName = string.Empty;
                var i = relativePath.IndexOf(Path.DirectorySeparatorChar);
                if (i != -1)
                    firstDirName = relativePath[0..i];

                var item = new ProjectItem(RootDir, relativePath);

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
}
