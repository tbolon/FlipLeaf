using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace FlipLeaf;

public sealed class WatcherService : IHostedService
{
    private readonly Site _site;
    private readonly ILiquidMarkup _liquid;
    private MemoryCache? _memCache;
    private FileSystemWatcher? _watcher;

    public WatcherService(Site site, ILiquidMarkup liquid)
    {
        _site = site;
        _liquid = liquid;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // wait for changes
        _memCache = new MemoryCache(new MemoryCacheOptions());
        _watcher = new FileSystemWatcher(Path.Combine(_site.RootDir))
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnChanged;
        _watcher.EnableRaisingEvents = true;

        return Task.CompletedTask;
    }

    private void OnChanged(object s, FileSystemEventArgs e)
    {
        if (e.Name == null) return;
        if (e.Name.StartsWith(KnownFolders.OutDir)) return;
        if (e.Name.StartsWith('.')) return;
        Console.WriteLine($"📝 {e.Name} changed");

        _memCache?.GetOrCreate(e.Name, CreateCacheEntry);
    }

    private object CreateCacheEntry(ICacheEntry cacheEntry)
    {
        Console.WriteLine($"🧲 {cacheEntry.Key} create cache entry");
        const int timeout = 500; // ms
        var cts = new CancellationTokenSource(timeout);
        cacheEntry.AddExpirationToken(new CancellationChangeToken(cts.Token)); // create
        cacheEntry.SlidingExpiration = TimeSpan.FromMilliseconds(timeout - 100);
        cacheEntry.RegisterPostEvictionCallback(OnCacheEntryExpired, state: cts);
        return cacheEntry.Key;
    }

    private async void OnCacheEntryExpired(object key, object? value, EvictionReason reason, object? state)
    {
        Console.WriteLine($"🌪️ {key} evicted");
        if (value == null || state == null) return;
        var name = (string)value;

        var i = name.IndexOf('\\');
        if (i == -1)
        {
            return; // skip root items
        }

        var folder = name[..i];
        name = name[(i + 1)..];
        switch (folder)
        {
            case KnownFolders.ContentDir:
                var item = _site.Content.FirstOrDefault(i => i.Name == name);
                if (item != null)
                {
                    await _site.RunPipeline(item);
                }
                break;

            case KnownFolders.IncludesDir:
            case KnownFolders.LayoutsDir:
                _liquid.LoadTemplates(_site);
                await _site.GenerateAll();
                break;
        }

        ((CancellationTokenSource?)state)?.Dispose();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _memCache?.Dispose();
        return Task.CompletedTask;
    }
}
