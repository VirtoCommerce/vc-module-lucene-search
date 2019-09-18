using System.IO;
using Microsoft.Extensions.Options;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.LuceneSearchModule.Tests
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTests")]
    public class LuceneSearchTests : SearchProviderTests
    {
        private readonly string _dataDirectoryPath = Path.Combine(Path.GetTempPath(), "lucene");

        protected override ISearchProvider GetSearchProvider()
        {
            var luceneOptions = Options.Create(new LuceneSearchOptions { Path = _dataDirectoryPath });
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "Lucene" });
            return new LuceneSearchProvider(luceneOptions, searchOptions);
        }
    }
}
