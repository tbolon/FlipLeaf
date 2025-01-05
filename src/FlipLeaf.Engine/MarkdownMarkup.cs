using System.Text;
using ColorCode;
using ColorCode.Common;
using ColorCode.Compilation.Languages;
using ColorCode.Styling;
using CsharpToColouredHTML.Core;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
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
            //builder.Extensions.AddIfNotAlready(new CodeSnippetExtension());

            builder
                .UseAdvancedExtensions();

            builder.Extensions.AddIfNotAlready(new CustomCodeBlockRenderer());

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

    public sealed class CustomCodeBlockRenderer : IMarkdownExtension
    {
        /// <inheritdoc/>
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        /// <inheritdoc/>
        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not TextRendererBase<HtmlRenderer> htmlRenderer)
                return;

            var codeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();

            if (codeBlockRenderer is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(codeBlockRenderer);
            }
            else
            {
                codeBlockRenderer = new CodeBlockRenderer();
            }

            htmlRenderer.ObjectRenderers.AddIfNotAlready(new ColorCodeBlockRenderer(codeBlockRenderer));
        }
    }

    internal sealed class ColorCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private readonly CodeBlockRenderer _fallback;

        public ColorCodeBlockRenderer(CodeBlockRenderer fallback)
        {
            _fallback = fallback;
        }

        protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
        {
            if (codeBlock is not FencedCodeBlock fencedCodeBlock || codeBlock.Parser is not FencedCodeBlockParser fencedCodeBlockParser)
            {
                _fallback.Write(renderer, codeBlock);
                return;
            }

            var languageId = fencedCodeBlock.Info!.Replace(fencedCodeBlockParser.InfoPrefix!, string.Empty);

            var language = string.IsNullOrWhiteSpace(languageId) ? null : Languages.FindById(languageId);

            if (language is null)
            {
                _fallback.Write(renderer, codeBlock);
                return;
            }

            var code = ExtractCode(codeBlock);

            string html;
            if (language.Id == LanguageId.CSharp)
            {
                Console.WriteLine("Using CsharpColourer");
                var csharpColourer = new CsharpColourer();
                var settings = new HTMLEmitterSettings
                {
                    AddLineNumber = false,
                    UseIframe = false,
                    UserProvidedCSS = "" // custom CSS defined in page header
                };

                html = csharpColourer.ProcessSourceCode(code, new HTMLEmitter(settings));
            }
            else

            {
                var formatter = new HtmlFormatter(StyleDictionary.DefaultLight);
                html = formatter.GetHtmlString(code, language);
            }

            renderer.Write(html);
        }

        /// <inheritdoc />
        public string ExtractCode(LeafBlock leafBlock)
        {
            var code = new StringBuilder();

            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            var lines = leafBlock.Lines.Lines ?? [];
            var totalLines = lines.Length;

            for (var index = 0; index < totalLines; index++)
            {
                var line = lines[index];
                var slice = line.Slice;

                if (slice.Text == null)
                {
                    continue;
                }

                var lineText = slice.Text.Substring(slice.Start, slice.Length);

                if (index > 0)
                {
                    code.AppendLine();
                }

                code.Append(lineText);
            }

            return code.ToString();
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
