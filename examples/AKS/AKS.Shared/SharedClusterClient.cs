using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proto;
using Proto.Cluster;
using Proto.Remote.GrpcCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AKS.Shared
{
    public class SharedClusterClientOptions
    {
        public const string Key = "ProtoClusterClient";
        public bool RestartOnFail { get; set; }
    }

    public class SharedClusterClient : ISharedClusterClient
    {
        private ILogger<SharedClusterClient> Logger { get; }
        private IDescriptorProvider DescriptorProvider { get; }
        private IClusterSettings ClusterSettings { get; }
        public Cluster? Cluster { get; private set; }
        private bool ClusterIsReady { get; set; }
        private ISharedClusterProviderFactory ClusterProviderFactory { get; }
        private Dictionary<string, int> Retries { get; } = new();
        private SharedClusterClientOptions ClientOptions { get; }

        public SharedClusterClient(
            ILogger<SharedClusterClient> logger,
            IDescriptorProvider descriptorProvider,
            IClusterSettings clusterSettings,
            ISharedClusterProviderFactory clusterProviderFactory,
            IOptions<SharedClusterClientOptions> clientOptionsAccessor)
        {
            Logger = logger;
            DescriptorProvider = descriptorProvider;
            ClusterSettings = clusterSettings;
            ClusterProviderFactory = clusterProviderFactory;
            ClientOptions = clientOptionsAccessor.Value;
        }


        public async Task Startup()
        {
            await CreateCluster();
        }

        public async Task Shutdown()
        {
            if (Cluster != null)
            {
                await Cluster.ShutdownAsync(true).ConfigureAwait(false);
                await Task.Delay(3000);
            }
        }

        public async Task<Cluster> CreateCluster()
        {
            try
            {
                Logger.LogInformation("Setting up Cluster without actors");
                Logger.LogInformation("ClusterName: " + ClusterSettings.ClusterName);
                Logger.LogInformation("PIDDatabaseName: " + ClusterSettings.PIDDatabaseName);
                Logger.LogInformation("PIDCollectionName: " + ClusterSettings.PIDCollectionName);

                var system = new ActorSystem();
                var clusterProvider = this.ClusterProviderFactory.CreateClusterProvider(Logger);

                var identity = MongoIdentityLookup.GetIdentityLookup(ClusterSettings.ClusterName,
                    ClusterSettings.PIDConnectionString,
                    ClusterSettings.PIDCollectionName,
                    ClusterSettings.PIDDatabaseName);

                var (clusterConfig, remoteConfig) = GenericClusterConfig.CreateClusterConfig(ClusterSettings, clusterProvider, identity, DescriptorProvider, Logger);

                _ = new GrpcCoreRemote(system, remoteConfig);
                Cluster = new Cluster(system, clusterConfig);

                await Cluster.StartClientAsync().ConfigureAwait(false);

                ClusterIsReady = true;

                Logger.LogInformation("Cluster Client ready");

                return Cluster;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SharedClusterClient failed");
                throw;
            }
        }

        public async Task<T?> RequestAsync<T>(string actorPath, string clusterKind, object cmd, CancellationToken token = default)
        {
            if (Cluster == null)
            {
                throw new ArgumentNullException(nameof(Cluster));
            }

            var counter = 0;

            while (!ClusterIsReady && counter < 40)
            {
                await Task.Delay(250).ConfigureAwait(false);
                counter++;
            }

            try
            {
                if (token == default)
                {
                    var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    token = tokenSource.Token;
                }

                var key = $"{actorPath}_{clusterKind}";
                var res = await Cluster.RequestAsync<T>(actorPath, clusterKind, cmd, token).ConfigureAwait(false);

                if (token.IsCancellationRequested && ClusterIsReady && ClientOptions.RestartOnFail)
                {
                    ClusterIsReady = false;
                    await RestartMe();
                    return await Retry<T>(actorPath, clusterKind, cmd, key);
                }

                if (!ClusterIsReady && ClientOptions.RestartOnFail)
                {
                    return await Retry<T>(actorPath, clusterKind, cmd, key);
                }

                Retries.Remove(key);

                return res;
            }
            catch (Exception x)
            {
                Logger.LogError(x, "Failed Request {Id}", actorPath);
                return default;
            }
        }

        private async Task<T?> Retry<T>(string actorPath, string clusterKind, object cmd, string key)
        {
            if (Retries.TryGetValue(key, out int value))
            {
                Interlocked.Increment(ref value);
            }
            else
            {
                Retries.Add(key, 1);
            }

            if (value > 5)
            {
                this.Logger.LogError("Request timeout for {Id}", actorPath);
                return default;
            }

            this.Logger.LogInformation("[{Client}] Retry request...", nameof(SharedClusterClient));
            await Task.Delay(value * 200);
            return await RequestAsync<T>(actorPath, clusterKind, cmd);
        }

        private async Task RestartMe()
        {
            Logger.LogWarning("[{Client}] Restarting", nameof(SharedClusterClient));
            try { await Shutdown(); }
            catch
            {
                // ignored
            }
            Cluster = null;
            await CreateCluster();
            await Task.Delay(3000);
        }
    }
}
