using Proto.Cluster;
using System.Threading.Tasks;

namespace AKS.Shared
{
    public interface IMainWorker
    {
        Task Run(Cluster cluster);
    }
}
