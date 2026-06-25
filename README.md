# FlipLeaf

FlipLeaf est un générateur de site statique (SSG) pour .NET.

Ce repository contient la nouvelle itération du projet (prototype en cours), inspirée de la première version `FlipLeaf.v1`, avec une architecture modulaire :

- **`FlipLeaf.Engine`** : moteur principal de génération (pipeline de transformation, gestion `content/layouts/includes`, mode watch).
- **`FlipLeaf.Sdk`** : SDK MSBuild pour simplifier la création d’un projet FlipLeaf.
- **`FlipLeaf.Tool`** : outil CLI (`flipleaf`) en cours de construction.
- **`FlipLeaf.Engine.Tests`** : projet de tests du moteur.

## Objectif

Générer un site statique à partir de fichiers de contenu, avec prise en charge de :

- **Markdown** (Markdig),
- **front matter YAML**,
- **templates Liquid** (layouts/includes),
- **post-traitements** (ex: génération de `sitemap.xml`).

## État du projet

Le projet est en **phase alpha / prototype**. L’API et les conventions peuvent évoluer.

## Cibles .NET

Selon les composants, le repository cible notamment :

- .NET 10
- .NET 8
- .NET Standard 2.0

## Démarrage rapide (`FlipLeaf.Engine`)

```bash
dotnet new console
dotnet add package FlipLeaf.Engine --prerelease
```

Créer un fichier `content/index.md`, puis utiliser un `Program.cs` similaire à :

```csharp
using FlipLeaf;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = SiteBuilder.CreateDefault(args)
    .AddYaml()
    .AddLiquid()
    .AddMarkdown();

var site = builder.Build();

// markdown rendering
site.Content.Add(async static (ISite s, Leaf input) =>
{
    if (input.Extension != ".md")
        return FlipStatus.Unhandled;

    var yaml = s.Services.GetRequiredService<IYamlMarkup>();
    var liquid = s.Services.GetRequiredService<ILiquidMarkup>();
    var markdown = s.Services.GetRequiredService<IMarkdownMarkup>();

    var content = input.ReadAllText();
    (content, var headers) = yaml.ParseHeader(content);
    (content, var context) = await liquid.RenderAsync(content, headers);
    content = markdown.Render(content, input);
    content = await liquid.ApplyLayoutAsync(content, context);

    return input.AsContentResult(content, Path.ChangeExtension(input.Name, ".html"));
});

// fallback: copy non-markdown files as-is
site.Content.Add(static leaf => leaf.AsCopyResult());

// optional post-process (example: sitemap.xml)
site.AddPostProcess(static s =>
{
    var ns = (XNamespace)"http://www.sitemaps.org/schemas/sitemap/0.9";
    new XElement(ns + "urlset",
        s.Content
            .Where(i => i.OutName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .Select(i => new XElement(ns + "url",
                new XElement(ns + "loc", "https://example.com/" + i.OutName.Replace('\\', '/'))))
    ).Save(Path.Combine(s.OutDir, "sitemap.xml"));
});

Console.WriteLine($"🍃 Starting on {site.RootDir}");
await site.RunAsync(args);
```

Les fichiers générés sont écrits dans `out`.

## Conventions par défaut

- La racine du site est détectée automatiquement depuis le répertoire courant.
- Le contenu est lu depuis `./content`.
- Les layouts sont lus depuis `./layouts`.
- Les includes sont lus depuis `./includes`.
- La sortie est écrite dans `./out`.

## Mode watch

Si le premier argument CLI est `watch`, le moteur surveille les changements de fichiers et régénère le site.

```bash
dotnet run -- watch
```

## Exemple réel : `coldwire.net` (FlipLeaf en sous-module)

Le projet `D:\code\perso\coldwire.net` utilise ce repository comme sous-module Git :

- `.gitmodules` déclare `flipleaf` pointant vers `https://github.com/tbolon/FlipLeaf.git`
- le projet importe directement :
  - `flipleaf/src/FlipLeaf.Sdk/Sdk/Sdk.props`
  - `flipleaf/src/FlipLeaf.Sdk/Sdk/Sdk.targets`
- `FlipLeafSdkProjectRef=True` permet de référencer les projets sources locaux (au lieu d’un package NuGet)
- `Program.cs` configure le pipeline (`AddYaml`, `AddLiquid`, `AddMarkdown`), transforme les `.md` en `.html`, copie les autres fichiers, puis génère un `sitemap.xml`
- la structure de contenu suit les conventions FlipLeaf : `content`, `layouts`, `includes`, sortie dans `out`

Ce cas montre un usage concret de FlipLeaf pour générer un site réel en s’appuyant sur le moteur directement depuis un sous-module.
