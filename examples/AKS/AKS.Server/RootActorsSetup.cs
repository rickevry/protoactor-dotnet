using System.Reflection;
using AKS.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace AKS.Server
{
    internal class RootActorsSetup : ISharedSetupRootActors
    {
        private IServiceProvider ServiceProvider { get; }
        private ILogger<RootActorsSetup> Logger { get; }

        public RootActorsSetup(IServiceProvider serviceProvider, ILogger<RootActorsSetup> logger)
        {
            ServiceProvider = serviceProvider;
            Logger = logger;
        }
        public ClusterConfig AddRootActors(ClusterConfig clusterConfig)
        {
            var iactorInfo = typeof(IActor).GetTypeInfo();

            var actorTypes = typeof(RootActorsSetup).Assembly
                .GetTypes()
                .Where(t => !t.IsInterface)
                .Where(t => iactorInfo.IsAssignableFrom(t.GetTypeInfo()))
                .ToList();

            foreach (var actorType in actorTypes)
            {
                var typeInfo = actorType.GetTypeInfo();
                var actorAttribute = typeInfo.GetCustomAttribute<ActorAttribute>();
                if (actorAttribute != null && !string.IsNullOrWhiteSpace(actorAttribute.Kind))
                {
                    var kind = actorAttribute.Kind;
                    var props = Props.FromProducer(() => (IActor) ServiceProvider.GetRequiredService(actorType));
                    clusterConfig = clusterConfig.WithClusterKind(kind, props);

                    Logger.LogInformation("'{Type}' is set up FromProducer with Kind '{Kind}'", actorType.Name, kind);
                }
                else
                {
                    Logger.LogWarning("'{Type}' must have ActorAttribute with a Kind value to be activated. Will be ignored.", actorType.Name);
                }
            }


            return clusterConfig;
        }
    }
}
