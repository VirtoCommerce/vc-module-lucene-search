using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SearchModule.Core.Extensions;

namespace VirtoCommerce.LuceneSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                serviceCollection.Configure<LuceneSearchOptions>(Configuration.GetSection($"Search:{ModuleConstants.ProviderName}"));
                serviceCollection.AddSingleton<LuceneSearchProvider>();
            }
        }

        public void PostInitialize(IApplicationBuilder serviceProvider)
        {
            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                serviceProvider.UseSearchProvider<LuceneSearchProvider>(ModuleConstants.ProviderName);
            }
        }

        public void Uninstall()
        {
            // Nothing to do here
        }
    }
}
