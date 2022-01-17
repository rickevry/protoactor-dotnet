using AKS.Shared.ProtoActorIdentity.Mongo;
using MongoDB.Driver;
using Proto.Cluster.Identity;


namespace AKS.Shared
{
    public class MongoIdentityLookup
    {
        public static IIdentityLookup GetIdentityLookup(string clusterName, string connectionString, string pidCollection, string pidDatabaseName)
        {
            var db = GetMongo(connectionString, pidDatabaseName);
            var identity = new IdentityStorageLookup(
                new Proto.Cluster.Identity.MongoDb.MongoIdentityStorage(clusterName, db.GetCollection<Proto.Cluster.Identity.MongoDb.PidLookupEntity>(pidCollection), 200)
            );
            return identity;
        }

        
        private static IMongoDatabase GetMongo(string connectionString, string databaseName)
        {
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            //settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            //settings.WaitQueueSize = 10000;
            
            var client = new MongoClient(settings);
            var database = client.GetDatabase(databaseName);
            return database;
        }
    }
}
