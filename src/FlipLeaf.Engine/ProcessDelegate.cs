namespace FlipLeaf;

public delegate Task ProcessDelegate<TOut>(LeafContext<TOut> context);