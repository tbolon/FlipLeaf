namespace FlipLeaf;

public sealed class PipelineStep
{
    public PipelineStep(ProcessDelegate processDelegate)
    {
        Delegate = processDelegate;
    }

    public ProcessDelegate Delegate { get; }
}
