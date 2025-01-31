namespace FlipLeaf;

public interface IWarmup
{
    Task Warmup(ISite site, CancellationToken cancellationToken);
}
