using System.IO;
using VirtoCommerce.CoreModule.Search.Tests;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.LuceneSearchModule.Data;
using Xunit;

namespace VirtoCommerce.LuceneSearchModule.Tests
{
    [Trait("Category", "CI")]
    public class LuceneSearchTests : SearchProviderTests
    {
        private readonly string _dataDirectoryPath = Path.Combine(Path.GetTempPath(), "lucene");

        protected override ISearchProvider GetSearchProvider()
        {
            return new LuceneSearchProvider(_dataDirectoryPath, "test");
        }
    }
}
