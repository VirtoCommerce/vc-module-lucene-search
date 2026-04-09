using Microsoft.Extensions.Options;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.LuceneSearchModule.Tests
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class LuceneInMemorySearchTests : SearchProviderTests
    {
        private static readonly ISearchProvider _sharedProvider = CreateProvider();

        private static ISearchProvider CreateProvider()
        {
            var luceneOptions = Options.Create(new LuceneSearchOptions { UseInMemory = true });
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "Lucene" });
            return new LuceneSearchProvider(luceneOptions, searchOptions);
        }

        protected override ISearchProvider GetSearchProvider()
        {
            return _sharedProvider;
        }
    }
}
