using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using System.Text;
using System.Text.RegularExpressions;

namespace FlipLeaf
{
    public interface IMarkdownMarkup
    {
        string Render(string markdown, SiteItem item);
    };

    public sealed class MarkdownMarkup : IMarkdownMarkup
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownMarkup()
        {
            var builder = new MarkdownPipelineBuilder();

            builder.UseAdvancedExtensions();

            // ``` renderer
            builder.Extensions.AddIfNotAlready<SyntaxHighlightCodeBlockExtension>();

            // :::code renderer
            builder.Extensions.AddIfNotAlready<SnippedCodeBlockExtension>();

            _pipeline = builder.Build();
        }

        public string Render(string markdown, SiteItem item)
        {
            using (var writer = new StringWriter())
            {
                var renderer = new FlipLeafPageHtmlRender(writer, item);

                _pipeline.Setup(renderer);

                var doc = Markdown.Parse(markdown, _pipeline);
                renderer.Render(doc);

                writer.Flush();
                return writer.ToString();
            }
        }
    }

    internal sealed class FlipLeafPageHtmlRender : HtmlRenderer
    {
        public FlipLeafPageHtmlRender(TextWriter writer, SiteItem item) : base(writer)
        {
            Item = item;
        }

        public SiteItem Item { get; }
    }

    /// <summary>
    /// An extension to add syntax highlighting for code blocks
    /// </summary>
    public sealed class SyntaxHighlightCodeBlockExtension : IMarkdownExtension
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

            // we remove the regular CodeBlockRenderer from te pipeline
            var codeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (codeBlockRenderer is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(codeBlockRenderer);
            }
            else
            {
                // no renderer registered, but we need one as fallback, so let's create it
                codeBlockRenderer = new CodeBlockRenderer();
            }

            // add our custom renderer
            htmlRenderer.ObjectRenderers.AddIfNotAlready(new ColorCodeBlockRenderer(codeBlockRenderer));
        }

        internal sealed class ColorCodeBlockRenderer(CodeBlockRenderer fallback) : HtmlObjectRenderer<CodeBlock>
        {
            protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
            {
                if (codeBlock is not FencedCodeBlock fencedCodeBlock || codeBlock.Parser is not FencedCodeBlockParser fencedCodeBlockParser)
                {
                    fallback.Write(renderer, codeBlock);
                    return;
                }

                var languageId = fencedCodeBlock.Info!.Replace(fencedCodeBlockParser.InfoPrefix!, string.Empty);

                // we use ColorCode to detect the language used
                var language = string.IsNullOrWhiteSpace(languageId) ? null : ColorCode.Languages.FindById(languageId);
                if (language is null)
                {
                    fallback.Write(renderer, codeBlock);
                    return;
                }

                var code = ExtractCode(codeBlock);

                string html;
                if (language.Id == ColorCode.Common.LanguageId.CSharp)
                {
                    // for C# we use CsharpToColouredHTML
                    html = RenderCSharp(code);
                }
                else
                {
                    // for the other cases let's use ColorCore
                    var formatter = new ColorCode.HtmlFormatter(ColorCode.Styling.StyleDictionary.DefaultLight);
                    html = formatter.GetHtmlString(code, language);
                }

                renderer.Write(html);
            }

            public static string RenderCSharp(string code)
            {
                var csharpColourer = new CsharpToColouredHTML.Core.CsharpColourer();
                var settings = new CsharpToColouredHTML.Core.HTMLEmitterSettings
                {
                    AddLineNumber = false,
                    UseIframe = false,
                    Optimize = false, // désactivé car simplifie les trop classes CSS
                    UserProvidedCSS = "" // custom CSS defined in page header
                };

                return csharpColourer.ProcessSourceCode(code, new CsharpToColouredHTML.Core.HTMLEmitter(settings));
            }

            public static string ExtractCode(LeafBlock leafBlock)
            {
                var code = new StringBuilder();
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
    }

    public sealed class SnippedCodeBlockExtension : IMarkdownExtension
    {
        public SnippedCodeBlockExtension()
        {
            Console.WriteLine("new Extension");
        }

        /// <inheritdoc/>
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            var parser = pipeline.BlockParsers.FindExact<CustomContainerParser>();
        }

        /// <inheritdoc/>
        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not FlipLeafPageHtmlRender htmlRenderer)
                return;

            var current = htmlRenderer.ObjectRenderers.FindExact<HtmlCustomContainerRenderer>();
            if (current is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(current);
            }

            htmlRenderer.ObjectRenderers.Add(new CodeCustomContainerRenderer(htmlRenderer.Item, current));
        }

        public class CodeCustomContainerRenderer : HtmlObjectRenderer<CustomContainer>
        {
            private readonly HtmlCustomContainerRenderer _default;
            private readonly SiteItem _item;
            private readonly string? _csxPath;
            private Dictionary<string, string>? _dict;

            public CodeCustomContainerRenderer(SiteItem item, HtmlCustomContainerRenderer? defaultRenderer)
            {
                _default = defaultRenderer ?? new();
                _item = item;

                _csxPath = _item.FullPath[..^item.Extension.Length] + ".csx";

                if (!File.Exists(_csxPath))
                {
                    //Console.WriteLine($"{_csxPath} missing !!");
                    _csxPath = null;
                    return;
                }

                var csx = File.ReadAllText(_csxPath);

                var startMatches = Regex.Matches(csx, @"^[\t ]*(// )?#(?<key>region)\s*(?<name>[^\r\n]+)\r?$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture);
                var endMatches = Regex.Matches(csx, @"^[\t ]*(// )?#(?<key>endregion)\r?$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

                var s = new Stack<(int, string)>();
                var ranges = new List<(string, int, int)>();

                foreach (Match match in startMatches.Cast<Match>().Concat(endMatches.Cast<Match>()).OrderBy(m => m.Index))
                {
                    Console.WriteLine($"Match: «{match.Value.TrimEnd()}»");
                    var isStart = match.Groups["key"].Value == "region";
                    if (isStart)
                    {
                        s.Push((match.Index + match.Length + 1, match.Groups["name"].Value));
                    }
                    else
                    {
                        if (!s.TryPop(out (int i, string name) begin))
                            break;

                        var j = match.Index;
                        ranges.Add((begin.name, begin.i, j));
                    }
                }

                _dict = new Dictionary<string, string>();

                foreach ((var name, var i, var j) in ranges)
                {
                    var block = csx.Substring(i, j - i).Trim('\r', '\n');
                    Console.WriteLine($"{i},{j}");
                    Console.WriteLine("---");
                    Console.WriteLine(block);
                    Console.WriteLine("---");

                    var lines = block.Split('\n').Select(l => l.TrimEnd());
                    var indent = lines.Where(l => !string.IsNullOrEmpty(l)).Min(l => l.TakeWhile(c => c == ' ').Count());
                    Console.WriteLine($"indent : {indent}");
                    _dict[name] = string.Join("\n", lines.Select(l => l.Length > indent ? l.Substring(indent) : l));

                    Console.WriteLine($"{i},{j}");
                    Console.WriteLine("---");
                    Console.WriteLine(_dict[name]);
                    Console.WriteLine("---");
                }
            }

            protected override void Write(HtmlRenderer renderer, CustomContainer obj)
            {
                if (obj.Info != "csx")
                {
                    _default.Write(renderer, obj);
                    return;
                }

                if (_csxPath == null)
                {
                    renderer.EnsureLine();
                    renderer.WriteLine("<code style=\"color:red\">!! Missing CSX File !!</code>");
                    return;
                }

                if (_dict == null)
                {
                    renderer.EnsureLine();
                    renderer.WriteLine("<code style=\"color:red\">!! No regions parsed!!</code>");
                    return;
                }

                var arg = obj.Arguments;

                if (arg == null)
                {
                    renderer.EnsureLine();
                    renderer.WriteLine("<code style=\"color:red\">!! Missing script block name !!</code>");
                    return;
                }

                if (!_dict.TryGetValue(arg, out var script))
                {
                    renderer.EnsureLine();
                    renderer.WriteLine($"<code style=\"color:red\">!! Unknown script block {arg} !!</code>");
                    return;
                }

                // if(script == null)
                // {

                // }

                var html = SyntaxHighlightCodeBlockExtension.ColorCodeBlockRenderer.RenderCSharp(script);

                renderer.Write(html);

                // renderer.EnsureLine();
                // if (renderer.EnableHtmlForBlock)
                // {
                //     renderer.Write("<div").WriteAttributes(obj).Write('>');
                //     renderer.Write("<pre><code>");
                // }

                // // We don't escape a CustomContainer
                // renderer.WriteChildren(obj);

                // if (renderer.EnableHtmlForBlock)
                // {
                //     renderer.Write("</code></pre>");
                //     renderer.WriteLine("</div>");
                // }
            }
        }
    }
}