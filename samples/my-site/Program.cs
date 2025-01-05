using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

Console.OutputEncoding = System.Text.Encoding.UTF8;

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
await GenerateAll();

// generate sitemap.xml
XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
new XElement(ns + "urlset",
    project.Content.Where(i => i.OutName.EndsWith(".html")).Select(i => new XElement(ns + "url",
        new XElement(ns + "loc", "https://my-site.com/" + i.OutName)))
).Save(Path.Combine(dest, "sitemap.xml"));

// TODO : generate rss

// wait for changes
using var memCache = new MemoryCache(new MemoryCacheOptions());
using var watcher = new FileSystemWatcher(Path.Combine(project.RootDir));
watcher.IncludeSubdirectories = true;
watcher.NotifyFilter = NotifyFilters.LastWrite;
watcher.Changed += (s, e) =>
{
    if (e.Name == null) return;
    if (e.Name.StartsWith("out")) return;
    Console.WriteLine($"📝 {e.Name} changed");
    memCache.GetOrCreate(e.Name, cacheEntry =>
    {
        Console.WriteLine($"🧲 {e.Name} create cache entry");
        const int timeout = 500; // ms
        var cts = new CancellationTokenSource(timeout);
        cacheEntry.AddExpirationToken(new CancellationChangeToken(cts.Token)); // create
        cacheEntry.SlidingExpiration = TimeSpan.FromMilliseconds(timeout - 100);
        cacheEntry.RegisterPostEvictionCallback(async (key, value, reason, state) =>
        {
            Console.WriteLine($"🌪️ {e.Name} evicted");
            if (value == null || state == null) return;
            var name = (string)value;

            var i = name.IndexOf('\\');
            if (i == -1)
            {
                return; // skip root items
            }

            var folder = name.Substring(0, i);
            name = name.Substring(i + 1);
            switch (folder)
            {
                case "content":
                    var item = project.Content.FirstOrDefault(i => i.Name == name);
                    if (item != null)
                    {
                        await GenerateContent(item);
                    }
                    break;

                case "includes":
                case "layouts":
                    liquid.LoadTemplates(project);
                    await GenerateAll();
                    break;
            }

            ((CancellationTokenSource?)state)?.Dispose();
        }, state: cts);
        return cacheEntry.Key;
    });
};

watcher.EnableRaisingEvents = true;

Console.WriteLine($"Press any key to exit...");
Console.ReadKey();

async Task GenerateAll()
{
    foreach (var item in project.Content)
    {
        await GenerateContent(item);
    }

}

async Task GenerateContent(ProjectItem item)
{
    Console.WriteLine($"💥 {item.Name} generating...");
    foreach (var step in contentPipeline)
    {
        if (await step.Invoke(item)) break;
    }
    Console.WriteLine($"✅ {item.Name} generated");

}