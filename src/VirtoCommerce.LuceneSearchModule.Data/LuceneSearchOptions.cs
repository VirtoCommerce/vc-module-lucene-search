namespace VirtoCommerce.LuceneSearchModule.Data;

public class LuceneSearchOptions
{
    /// <summary>
    /// Path to the directory where Lucene indices are stored.
    /// Required when UseInMemory is false.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// When true, uses in-memory RAMDirectory instead of FSDirectory.
    /// Useful for testing purposes. Default is false.
    /// </summary>
    public bool UseInMemory { get; set; }
}
