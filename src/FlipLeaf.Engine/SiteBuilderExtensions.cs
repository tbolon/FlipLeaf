using Microsoft.Extensions.DependencyInjection;

namespace FlipLeaf;

public static class SiteBuilderExtensions
{
    public static SiteBuilder AddYaml(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<IYamlMarkup, YamlMarkup>();
        return @this;
    }

    public static SiteBuilder AddLiquid(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<ILiquidMarkup, LiquidMarkup>();
        return @this;
    }

    public static SiteBuilder AddMarkdown(this SiteBuilder @this)
    {
        @this.Services.AddSingleton<IMarkdownMarkup, MarkdownMarkup>();
        return @this;
    }
}
