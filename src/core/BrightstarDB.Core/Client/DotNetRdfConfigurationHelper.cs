using System;
using VDS.RDF;
using VDS.RDF.Configuration;
#if PORTABLE
using BrightstarDB.Portable.Adaptation;
using BrightstarDB.Storage;
#endif

namespace BrightstarDB.Client
{
    internal static class DotNetRdfConfigurationHelper
    {
        private static bool _factoriesRegistered;
        private static readonly object _factoryLock = new object();

        private static void EnsureObjectFactoriesRegistered()
        {
            if (_factoriesRegistered) return;
            lock (_factoryLock)
            {
                if (_factoriesRegistered) return;
                // Register dotNetRdf.Client StorageFactory if available (needed for FusekiConnector etc. in 3.x)
                try
                {
                    var storageFactoryType = Type.GetType("VDS.RDF.Configuration.StorageFactory, dotNetRdf.Client", false);
                    if (storageFactoryType != null)
                    {
                        ConfigurationLoader.AddObjectFactory((IObjectFactory)Activator.CreateInstance(storageFactoryType));
                    }
                }
                catch
                {
                    // dotNetRdf.Client not available — ignore
                }
                _factoriesRegistered = true;
            }
        }

#if PORTABLE
        public static IGraph LoadConfiguration(string configurationPath)
        {
            var pm = PlatformAdapter.Resolve<IPersistenceManager>();
            ConfigurationLoader.PathResolver = new DotNetRdfConfigurationPathResolver(configurationPath);
            using (var stream = pm.GetInputStream(configurationPath))
            {
                return ConfigurationLoader.LoadConfiguration(configurationPath, new Uri(configurationPath), stream);
            }
        }
#else
        public static IGraph LoadConfiguration(string configurationPath)
        {
            EnsureObjectFactoriesRegistered();
            ConfigurationLoader.PathResolver = new DotNetRdfConfigurationPathResolver(configurationPath);
            return ConfigurationLoader.LoadConfiguration(configurationPath);
        }
#endif

        public static object GetConfigurationObject(IGraph configurationGraph, string id)
        {
#if PORTABLE
            // Bug in portable class library - if id is a fragment identifier the URI is not combined correctly
            Uri targetUri = id.StartsWith("#") ? new Uri(configurationGraph.BaseUri + id) : new Uri(configurationGraph.BaseUri, id);
#else
            var targetUri = new Uri(configurationGraph.BaseUri, id);
#endif
            var configNode = configurationGraph.GetUriNode(targetUri);
            return configNode == null ? null : ConfigurationLoader.LoadObject(configurationGraph, configNode);
        }
    }
}