var dir = Environment.CurrentDirectory;

var dest = Path.Combine(dir, "out");
if (!Directory.Exists(dest))
    Directory.CreateDirectory(dest);

var files = Directory.GetFiles(Path.Combine(dir, "content"), "*.*");

foreach (var file in files)
{
    switch (Path.GetExtension(file).ToLowerInvariant())
    {
        case ".md":
            var md = File.ReadAllText(file);
            var output = Markdig.Markdown.ToHtml(md);
            File.WriteAllText(Path.Combine(dest, Path.GetFileNameWithoutExtension(file) + ".html"), output);
            break;

        default:
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            break;
    }
}