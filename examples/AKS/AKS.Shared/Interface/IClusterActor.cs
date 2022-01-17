using Proto;

namespace AKS.Shared
{
    public interface IClusterActor : IActor
    {
        public string ClusterKind { get; }
    }
}
