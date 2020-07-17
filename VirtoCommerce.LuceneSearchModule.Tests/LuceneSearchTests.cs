using System.IO;
using System.Threading.Tasks;
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
            return new LuceneSearchProvider(new LuceneSearchProviderSettings(_dataDirectoryPath, "test"));
        }
        public override Task CanSortByGeoDistance()
        {
            // Base test disabled, Lucene can't correctly sort GEOs.
            // In v3 this test disabled earlier too.
            return Task.CompletedTask;
        }
    }
}
