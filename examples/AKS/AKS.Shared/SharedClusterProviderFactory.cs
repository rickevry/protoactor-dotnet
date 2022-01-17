using AKS.Shared.Settings;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Kubernetes;
using System;

namespace AKS.Shared
{
    public class SharedClusterProviderFactory : ISharedClusterProviderFactory
    {

        private readonly IClusterSettings _clusterSettings;
        private readonly KubernetesClusterOptions _kubernetesClusterOptions;

        public SharedClusterProviderFactory(IClusterSettings clusterSettings, IOptions<KubernetesClusterOptions> kubernetesClusterOptionAccessor)
        {
            _clusterSettings = clusterSettings;
            _kubernetesClusterOptions = kubernetesClusterOptionAccessor.Value;
        }

        public IClusterProvider CreateClusterProvider(ILogger logger)
        {
            try
            {
                if (this._clusterSettings.UseConsul)
                {
                    return UseConsul(logger);
                }
                return UseKubernetes(logger);
            }
            catch
            {
                return UseConsul(logger);
            }
        }

        private IClusterProvider UseConsul(ILogger logger)
        {
            logger.LogDebug("Running with Consul Provider");
            return new ConsulProvider(new ConsulProviderConfig(), c => { c.Address = new Uri(_clusterSettings.ConsulUri); });
        }

        private IClusterProvider UseKubernetes(ILogger logger)
        {
            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            logger.LogDebug("Running with Kubernetes Provider", kubernetes.BaseUri);
            //KubernetesProviderConfig config = new();
            //if (_kubernetesClusterOptions != null)
            //{
	           // config = new KubernetesProviderConfig(_kubernetesClusterOptions.WatchTimeoutSeconds, _kubernetesClusterOptions.DeveloperLogging);
            //}
            //return new KubernetesProvider(kubernetes, config);
            return new KubernetesProvider(kubernetes);
        }
    }
}
