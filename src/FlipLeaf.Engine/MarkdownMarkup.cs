using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdown.ColorCode;
using Markdown.ColorCode.CSharpToColoredHtml;

namespace FlipLeaf
{
    public interface IMarkdownMarkup
    {
        string Render(string markdown);
    };

    public sealed class MarkdownMarkup : IMarkdownMarkup
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownMarkup()
        {
            var builder = new MarkdownPipelineBuilder();
            builder.Extensions.AddIfNotAlready(new CodeSnippetExtension());

            builder
                .UseAdvancedExtensions()
                .UseColorCodeWithCSharpToColoredHtml(styleDictionary: ColorCode.Styling.StyleDictionary.DefaultLight);

            //builder.Extensions.AddIfNotAlready(new WikiLinkExtension() { Extension = ".md" });
            //builder.Extensions.AddIfNotAlready(new CustomLinkInlineRendererExtension(settings.BaseUrl));

            _pipeline = builder.Build();
        }

        public string Render(string markdown)
        {
            using (var writer = new StringWriter())
            {
                var renderer = new HtmlRenderer(writer);

                _pipeline.Setup(renderer);

                renderer.ObjectRenderers.Insert(0, new CodeCustomContainerRenderer());

                var doc = Markdig.Markdown.Parse(markdown, _pipeline);

                renderer.Render(doc);

                writer.Flush();

                return writer.ToString();
            }
        }
    }

    public class CodeCustomContainerRenderer : HtmlObjectRenderer<CustomContainer>
    {
        private readonly HtmlCustomContainerRenderer _default;

        public CodeCustomContainerRenderer()
        {
            _default = new HtmlCustomContainerRenderer();
        }

        protected override void Write(HtmlRenderer renderer, CustomContainer obj)
        {
            if (obj.Info != "code")
            {
                _default.Write(renderer, obj);
                return;
            }

            renderer.EnsureLine();
            if (renderer.EnableHtmlForBlock)
            {
                renderer.Write("<div").WriteAttributes(obj).Write('>');
                renderer.Write("<pre><code>");
            }



            // We don't escape a CustomContainer
            renderer.WriteChildren(obj);

            if (renderer.EnableHtmlForBlock)
            {
                renderer.Write("</code></pre>");
                renderer.WriteLine("</div>");
            }
        }
    }

    public class CodeSnippetExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }
    }
}
