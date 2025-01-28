namespace FlipLeaf
{
    public interface IProject
    {
        string RootDir { get; }

        IReadOnlyList<SiteItem> Layouts { get; }

        IReadOnlyList<SiteItem> Content { get; }

        IReadOnlyList<SiteItem> Includes { get; }

        void Populate();
    }

    public class Project : IProject
    {
        private readonly List<SiteItem> _layouts = [];
        private readonly List<SiteItem> _content = [];
        private readonly List<SiteItem> _includes = [];

        public Project(string rootDir)
        {
            RootDir = Path.GetFullPath(rootDir);
        }

        public string RootDir { get; }

        public IReadOnlyList<SiteItem> Layouts => _layouts;

        public IReadOnlyList<SiteItem> Content => _content;

        public IReadOnlyList<SiteItem> Includes => _includes;

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

                var item = new SiteItem(RootDir, relativePath);

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
