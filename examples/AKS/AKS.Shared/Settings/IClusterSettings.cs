namespace AKS.Shared
{
    public interface IClusterSettings
    {
        string ClusterName { get; set; }
        string ClusterHost { get; set; }
        int ClusterPort { get; set; }
        string PIDConnectionString { get; set; }
        string PIDCollectionName { get; set; }
        string PIDDatabaseName { get; set; }
        string ConsulUri { get; set; }
        bool UseConsul { get; set; }
    }
}
