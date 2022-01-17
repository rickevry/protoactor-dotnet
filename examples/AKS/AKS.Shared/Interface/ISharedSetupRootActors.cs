using Proto.Cluster;

namespace AKS.Shared
{
    public interface ISharedSetupRootActors
    {
        ClusterConfig AddRootActors(ClusterConfig clusterConfig);
    }
}
