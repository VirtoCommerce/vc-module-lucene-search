using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.LuceneSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            var provider = Configuration.GetValue<string>("Search:Provider");

            if (provider.EqualsInvariant("Lucene"))
            {
                serviceCollection.AddOptions<LuceneSearchOptions>().Bind(Configuration.GetSection("Search:Lucene")).ValidateDataAnnotations();
                serviceCollection.AddSingleton<ISearchProvider, LuceneSearchProvider>();
            }
        }

        public void PostInitialize(IApplicationBuilder serviceProvider)
        {
        }

        public void Uninstall()
        {
        }
    }
}
