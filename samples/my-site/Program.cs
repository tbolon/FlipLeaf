var dir = Environment.CurrentDirectory;
dir = Project.DetectRootDir(dir);

var project = new Project(dir);
project.Populate();

var dest = Path.Combine(dir, "out");
if (!Directory.Exists(dest))
    Directory.CreateDirectory(dest);

var yaml = new YamlMarkup();
var liquid = new LiquidMarkup(yaml);
var md = new MarkdownMarkup();

liquid.LoadTemplates(project);

foreach (var file in project.Content)
{
    switch (file.Extension.ToLowerInvariant())
    {
        case ".md":
            var content = file.ReadAllText();
            var headers = yaml.ParseHeader(content, out content);
            content = await liquid.RenderAsync(content, headers, out var context);
            content = md.Render(content);
            content = await liquid.ApplyLayoutAsync(content, context);
            File.WriteAllText(Path.Combine(dest, Path.GetFileNameWithoutExtension(file.Name) + ".html"), content);
            break;

        default:
            File.Copy(file.FullPath, Path.Combine(dest, file.Name));
            break;
    }
}