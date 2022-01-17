using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace AKS.Shared
{
    public interface ISharedClusterProviderFactory
    {
        IClusterProvider CreateClusterProvider(ILogger logger);
    }
}
