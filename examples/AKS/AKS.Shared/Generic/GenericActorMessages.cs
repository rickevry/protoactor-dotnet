namespace AKS.Shared
{
    public class GarbageCollectCmd
    {
        public GarbageCollectCmd()
        {
        }
    }

    public class LoadStateCmd
    {
        public string Tenant { get; }
        public string Name { get; }
        public string Eid { get; }
        public string ObjectId { get; }

        public LoadStateCmd(string tenant, string name, string eid, string objectId)
        {
            Tenant = tenant;
            Name = name;
            Eid = eid;
            ObjectId = objectId;
        }
    }
}
