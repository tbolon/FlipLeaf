namespace FlipLeaf;

public sealed class HeaderFieldDictionary : Dictionary<string, object?>
{
    public HeaderFieldDictionary()
        : base(StringComparer.Ordinal)
    {
    }

    public T[] GetArray<T>(string name) => [.. GetCollection<T>(name)];

    public IEnumerable<T> GetCollection<T>(string name)
    {
        if (this.GetValueOrDefault(name) is not IEnumerable<object> objects)
        {
            return [];
        }

        return objects.Cast<T>();
    }
}