namespace AKS.Shared
{
    public interface IGenericDataBaseSettings
    {
        string ConnectionName { get; set; }
        string CollectionName { get; set; }
        string ConnectionString { get; set; }
        string DatabaseName { get; set; }
    }
}
