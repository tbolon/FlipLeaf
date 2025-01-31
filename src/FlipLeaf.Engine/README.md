# FlipLeaf.Engine

Early version; still a prototype.

FlipLeaf is a(nother) static source generator for .NET.

This engine allows you to create a new console app and generate a new site using these commands:

```
dotnet new console
dotnet add package FlipLeaf.Engine --version 1.0.0-alpha-10
```

You can create a `index.md` file in a "content" directory,

Then edit the Program.cs file to set this content:

```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8; // required for emoticons

var builder = SiteBuilder.CreateDefault(args);

builder
    .AddYaml()
    .AddLiquid()
    .AddMarkdown();

var site = builder.Build();

// markdown rendering
site.AddToPipeline(async static (ISite s, Leaf input) =>
{
    if (input.Extension != ".md") return input.FlipToNothing();

    // dependencies
    var yaml = s.Services.GetRequiredService<IYamlMarkup>();
    var liquid = s.Services.GetRequiredService<ILiquidMarkup>();
    var md = s.Services.GetRequiredService<IMarkdownMarkup>();

    // render
    var content = input.ReadAllText();
    (content, var headers) = yaml.ParseHeader(content);
    (content, var context) = await liquid.RenderAsync(content, headers);
    content = md.Render(content, input);
    content = await liquid.ApplyLayoutAsync(content, context);

    // write output
    return input.FlipToContent(content, Path.GetFileNameWithoutExtension(input.Name) + ".html");
});

// default action: copy file as-is
site.AddToPipeline(static x => x.FlipToCopy());

// generate sitemap.xml
site.AddPostProcess(static s =>
{
    var ns = (XNamespace)"http://www.sitemaps.org/schemas/sitemap/0.9";
    new XElement(ns + "urlset",
        s.Content.Where(i => i.OutName.EndsWith(".html")).Select(i => new XElement(ns + "url",
            new XElement(ns + "loc", "https://coldwire.net/" + i.OutName)))
    ).Save(Path.Combine(s.OutDir, "sitemap.xml"));
});

// TODO : generate rss

// RUN !
Console.WriteLine($"🍃 Starting on {site.RootDir}");
await site.RunAsync(args);
```

Your website will be generated in the "out" directory.