using System.Web.Hosting;
using Microsoft.Practices.Unity;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.LuceneSearchModule.Web
{
    public class Module : ModuleBase
    {
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        public override void Initialize()
        {
            base.Initialize();

            var searchConnection = _container.Resolve<ISearchConnection>();

            if (searchConnection?.Provider?.EqualsInvariant("Lucene") == true)
            {
                var dataDirectoryPath = HostingEnvironment.MapPath(searchConnection["DataDirectoryPath"] ?? searchConnection["server"]);
                var luceneSettings = new LuceneSearchProviderSettings(dataDirectoryPath, searchConnection.Scope);

                _container.RegisterInstance(luceneSettings);
                _container.RegisterType<ISearchProvider, LuceneSearchProvider>(new ContainerControlledLifetimeManager());
            }
        }
    }
}
