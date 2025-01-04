using System.Xml.Linq;

var dir = Environment.CurrentDirectory;
dir = Project.DetectRootDir(dir);

var project = new Project(dir);
project.Populate();

var dest = Path.Combine(dir, "out");
if (!Directory.Exists(dest))
    Directory.CreateDirectory(dest);

var contentPipeline = new List<Func<ProjectItem, Task<bool>>>();

// markdown rendering
var yaml = new YamlMarkup();
var liquid = new LiquidMarkup(yaml);
liquid.LoadTemplates(project);
var md = new MarkdownMarkup();
contentPipeline.Add(async (ProjectItem file) =>
{
    if (file.Extension != ".md") return false;

    // render
    var content = file.ReadAllText();
    (content, var headers) = yaml.ParseHeader(content);
    (content, var context) = await liquid.RenderAsync(content, headers);
    content = md.Render(content);
    content = await liquid.ApplyLayoutAsync(content, context);

    // write output
    file.OutName = Path.GetFileNameWithoutExtension(file.Name) + ".html";
    File.WriteAllText(Path.Combine(dest, file.OutName), content);
    return true;
});

// default action: copy file as-is
contentPipeline.Add((ProjectItem file) =>
{
    File.Copy(file.FullPath, Path.Combine(dest, file.Name), overwrite: true);
    return Task.FromResult(true);
});

// generate content
foreach (var item in project.Content)
{
    foreach (var step in contentPipeline)
    {
        if (await step.Invoke(item)) break;
    }
}

// generate sitemap.xml
XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
new XElement(ns + "urlset",
    project.Content.Where(i => i.OutName.EndsWith(".html")).Select(i => new XElement(ns + "url",
        new XElement(ns + "loc", "https://my-site.com/" + i.OutName)))
).Save(Path.Combine(dest, "sitemap.xml"));

// TODO : generate rss