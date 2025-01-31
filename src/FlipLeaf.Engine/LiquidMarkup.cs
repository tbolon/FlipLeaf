using Fluid.Filters;
using Fluid.Values;
using Fluid;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace FlipLeaf
{
    public interface ILiquidMarkup
    {
        void LoadTemplates(IProject project);

        ValueTask<(string content, TemplateContext templateContext)> RenderAsync(string content, HeaderFieldDictionary headers);

        ValueTask<string> ApplyLayoutAsync(string source, TemplateContext sourceContext);
    }

    public class LiquidMarkup : ILiquidMarkup, IWarmup
    {
        private readonly Dictionary<string, LiquidLayout> _layouts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LiquidInclude> _includes = new(StringComparer.OrdinalIgnoreCase);
        private readonly FlipLeafFileProvider _fileProvider;
        private readonly string _baseUrl;
        private readonly IYamlMarkup _yaml;

        public LiquidMarkup(IYamlMarkup yaml)
        {
            _baseUrl = ".";
            _fileProvider = new FlipLeafFileProvider(_includes);
            _yaml = yaml;
        }

        public Task Warmup(ISite site, CancellationToken cancellation)
        {
            LoadTemplates(site.Project);
            return Task.CompletedTask;
        }

        public void LoadTemplates(IProject project)
        {
            // populate Layouts
            _layouts.Clear();
            foreach (var file in project.Layouts)
            {
                var content = file.ReadAllText();

                HeaderFieldDictionary yamlHeader;
                try
                {
                    (content, yamlHeader) = _yaml.ParseHeader(content);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Layout {file} is invalid: YAML errors", nameof(file), ex);
                }

                var parser = new FluidParser();
                parser.RegisterEmptyTag("body", async (writer, encoder, context) =>
                {
                    if (context.AmbientValues.TryGetValue("body", out var body))
                    {
                        await writer.WriteAsync((string)body).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new ParseException("Could not render body, Layouts can't be evaluated directly.");
                    }

                    return Fluid.Ast.Completion.Normal;
                });

                IFluidTemplate template;
                try
                {
                    template = parser.Parse(content);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Layout {file} in invalid: Liquid errors", nameof(file), ex);
                }

                var layout = new LiquidLayout(file, yamlHeader, template);
                _layouts.Add(layout.Name, layout);
            }

            // populate includes
            _includes.Clear();
            foreach (var file in project.Includes)
            {
                byte[] content;

                using (var openRead = file.OpenRead())
                using (var ms = new MemoryStream())
                {
                    openRead.CopyTo(ms);
                    content = ms.ToArray();
                }

                var include = new LiquidInclude(file, content);
                _includes.Add(file.Name, include);
            }
        }

        public async ValueTask<(string content, TemplateContext templateContext)> RenderAsync(string content, HeaderFieldDictionary headers)
        {
            // parse content as template
            var parser = new FluidParser();
            var pageTemplate = parser.Parse(content);

            // prepare context
            var templateContext = CreateTemplateContext();
            templateContext.SetValue(KnownVariables.Page, headers);
            //templateContext.SetValue(KnownVariables.Site, website);

            // render content
            var newContent = await pageTemplate.RenderAsync(templateContext);
            return (newContent, templateContext);
        }

        public async ValueTask<string> ApplyLayoutAsync(string source, TemplateContext sourceContext)
        {
            var pageItem = sourceContext.GetValue(KnownVariables.Page);
            var layout = await pageItem.GetValueAsync(KnownVariables.Layout, sourceContext).ConfigureAwait(false);
            var layoutName = layout.ToStringValue();
            if (string.IsNullOrEmpty(layoutName))
            {
                return source; // no layout field, ends here
            }

            return await ApplyLayoutAsync(source, sourceContext, layoutName, 0).ConfigureAwait(false);
        }

        private async ValueTask<string> ApplyLayoutAsync(string source, TemplateContext sourceContext, string layoutName, int level)
        {
            if (level >= 5)
            {
                // no more than x levels of nesting
                throw new NotSupportedException($"Recursive layouts are limited to 5 levels of recursion");
            }


            // load layout
            if (!_layouts.TryGetValue(layoutName, out var layout))
            {
                return source;
            }

            // create new TemplateContext for the layout
            var layoutContext = CreateTemplateContext();
            layoutContext.SetValue(KnownVariables.Page, sourceContext.GetValue(KnownVariables.Page));
            layoutContext.SetValue(KnownVariables.Layout, layout.YamlHeader);
            layoutContext.SetValue("body", source);
            //layoutContext.SetValue(KnownVariables.Site, website);


            // render layout
            source = await layout.RenderAsync(layoutContext).ConfigureAwait(false);

            if (!layout.YamlHeader.TryGetValue(KnownVariables.Layout, out var outerLayoutObject) || !(outerLayoutObject is string outerLayoutFile))
            {
                // no recusrive layout, we stop here
                return source;
            }

            // recursive layout...
            return await ApplyLayoutAsync(source, sourceContext, outerLayoutFile, level + 1).ConfigureAwait(false);
        }

        private TemplateContext CreateTemplateContext()
        {
            var options = new TemplateOptions();
            //options.MemberAccessStrategy.Register(typeof(Website.IWebsite));
            //options.MemberAccessStrategy.Register(typeof(Website.Website));
            options.Filters.AddFilter("relative_url", RelativeUrlFilterAsync);
            options.FileProvider = _fileProvider;

            return new TemplateContext(options);
        }

        private ValueTask<FluidValue> RelativeUrlFilterAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return string.IsNullOrEmpty(_baseUrl)
                ? ValueTask.FromResult(input)
                : StringFilters.Prepend(input, new FilterArguments(new StringValue(_baseUrl)), context);
        }

        public class LiquidFile(Leaf file)
        {
            public virtual string Name => File.RelativePath;

            protected Leaf File { get; } = file;

            public override int GetHashCode() => File.GetHashCode();

            public override bool Equals(object? obj) => obj switch
            {
                LiquidFile item => File.Equals(item.File),
                _ => base.Equals(obj)
            };
        }

        public class LiquidInclude(Leaf file, byte[] content) : LiquidFile(file)
        {
            public new Leaf File => base.File;

            public byte[] Content { get; } = content;
        }

        public class LiquidLayout(Leaf file, HeaderFieldDictionary yamlHeader, IFluidTemplate template) : LiquidFile(file)
        {

            public override string Name { get; } = Path.GetFileNameWithoutExtension(file.Name);

            public HeaderFieldDictionary YamlHeader { get; } = yamlHeader;

            public ValueTask<string> RenderAsync(TemplateContext context) => template.RenderAsync(context);

            public override int GetHashCode() => Name.GetHashCode();

            public override string ToString() => Name;
        }

        private class FlipLeafFileProvider(IDictionary<string, LiquidInclude> includes) : IFileProvider
        {
            public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

            public IFileInfo GetFileInfo(string subpath) => includes.TryGetValue(subpath, out var include) ? new IncludeFileInfo(include) : new NotFoundFileInfo(subpath);

            public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

            private class IncludeFileInfo(LiquidInclude include) : IFileInfo
            {
                private readonly byte[] _content = include.Content;

                public bool Exists => true;

                public long Length => 0;

                public string PhysicalPath { get; } = include.File.FullPath;

                public string Name { get; } = include.File.Name;

                public DateTimeOffset LastModified => DateTime.MinValue;

                public bool IsDirectory => false;

                public Stream CreateReadStream() => new MemoryStream(_content);
            }
        }
    }
}
