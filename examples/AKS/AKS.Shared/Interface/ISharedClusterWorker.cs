using System;
using Proto.Cluster;
using System.Threading.Tasks;

namespace AKS.Shared
{
    public interface ISharedClusterWorker
    {
        Task<Cluster> CreateCluster();

        Lazy<Cluster> Cluster { get; }

        Task Shutdown();
    }
}
