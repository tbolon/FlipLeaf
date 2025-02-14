namespace FlipLeaf;

public sealed class PipelineStep<TOut>
{
    public PipelineStep(ProcessDelegate<TOut> processDelegate)
    {
        Delegate = processDelegate;
    }

    public ProcessDelegate<TOut> Delegate { get; }
}