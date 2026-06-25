#:sdk FlipLeaf.Sdk@1.0.0-alpha-02

using FlipLeaf;
using System.Xml.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = SiteBuilder.CreateDefault(args)
    .AddYaml()
    .AddLiquid()
    .AddMarkdown();

var site = builder.Build();

site.Content.Add(async static (LeafContext<LeafFileOutput> ctx) =>
{
    var site = ctx.Site;
    var input = ctx.Input;
    var output = ctx.Output;

    if (input.Extension != ".md")
    {
        output.Status = LeafOutputStatus.Unhandled;
        return;
    }

    var yaml = (IYamlMarkup?)site.Services.GetService(typeof(IYamlMarkup)) ?? throw new InvalidOperationException("IYamlMarkup service is missing");
    var liquid = (ILiquidMarkup?)site.Services.GetService(typeof(ILiquidMarkup)) ?? throw new InvalidOperationException("ILiquidMarkup service is missing");
    var markdown = (IMarkdownMarkup?)site.Services.GetService(typeof(IMarkdownMarkup)) ?? throw new InvalidOperationException("IMarkdownMarkup service is missing");

    var content = input.ReadAllText();
    (content, var headers) = yaml.ParseHeader(content);
    (content, var context) = await liquid.RenderAsync(content, headers);
    content = markdown.Render(content, input);
    content = await liquid.ApplyLayoutAsync(content, context);

    output.ChangeExtension(".html");
    await output.AsContentResult(content).Execute(ctx);
});

site.Content.Add(static leaf => leaf.AsCopyResult());

site.AddPostProcess(static s =>
{
    var ns = (XNamespace)"http://www.sitemaps.org/schemas/sitemap/0.9";
    new XElement(ns + "urlset",
        s.Content
            .Where(i => i.OutName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .Select(i => new XElement(ns + "url",
                new XElement(ns + "loc", "https://flipleaf.dev/" + i.OutName.Replace('\\', '/'))))
    ).Save(Path.Combine(s.OutDir, "sitemap.xml"));
});

Console.WriteLine($"🍃 FlipLeaf official sample: {site.RootDir}");
await site.RunAsync(args);
