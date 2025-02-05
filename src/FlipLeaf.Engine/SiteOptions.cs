namespace FlipLeaf;

public class SiteOptions
{
    /// <summary>
    /// Path for which all the other directories are related to.
    /// <see cref="Environment.CurrentDirectory" />
    /// </summary>
    public string? RootDir { get; set; }

    /// <summary>
    /// Path where the site content is located.
    /// Defaults to "./content".
    /// </summary>
    public string ContentDir { get; set; } = KnownFolders.ContentDir;
}
