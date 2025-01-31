namespace FlipLeaf;

public sealed class LeafContext
{
    public LeafContext(Site site, Leaf input)
    {
        Site = site;
        Input = input;
    }

    public Site Site { get; }

    public Leaf Input { get; }

    public LeafOutput? Output { get; set; }
}