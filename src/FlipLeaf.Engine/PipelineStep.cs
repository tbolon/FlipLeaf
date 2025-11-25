using System.Collections;

namespace FlipLeaf;

public sealed class PipelineStep<TOut>
{
    public PipelineStep(ProcessDelegate<TOut> processDelegate)
    {
        Delegate = processDelegate;
    }

    public ProcessDelegate<TOut> Delegate { get; }
}

public abstract class PipelineStepCollection
{
    private readonly Type _outType;

    protected PipelineStepCollection(Type outType)
    {
        _outType = outType;
    }
}

public sealed class PipelineStepCollection<TOut> : PipelineStepCollection, IEnumerable<PipelineStep<TOut>>
{
    private readonly List<PipelineStep<TOut>> _steps = [];

    public PipelineStepCollection() : base(typeof(TOut))
    {
    }

    public PipelineStep<TOut> Add(ProcessDelegate<TOut> task)
    {
        var step = new PipelineStep<TOut>(task);
        _steps.Add(step);
        return step;
    }

    public IEnumerator<PipelineStep<TOut>> GetEnumerator() => _steps.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
