namespace FlipLeaf;

/// <summary>
/// A process context, where a leaf input is mean to be transformed into an <typeparamref name="TOut"/>.
/// </summary>
/// <typeparam name="TOut">Type of the leaf output.</typeparam>
public class LeafContext<TOut>(Site site, Leaf input, TOut output)
{
    /// <summary>
    /// Gets the site containing this leaf input.
    /// </summary>
    public Site Site { get; } = site;

    /// <summary>
    /// Gets the input leaf.
    /// </summary>
    public Leaf Input { get; } = input;

    /// <summary>
    /// Gets the output leaf.
    /// </summary>
    public TOut Output { get; } = output;
}