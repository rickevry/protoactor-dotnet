using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Remote.GrpcCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
//using Ubiquitous.Metrics;

namespace AKS.Shared
{
    public class SharedClusterWorkerOptions
    {
        public const string Key = "ProtoCluster";
        public bool RestartOnFail { get; set; }
    }

    public class SharedClusterWorker : ISharedClusterWorker
    {
        private Cluster? _cluster;
        public Lazy<Cluster> Cluster => new(() => _cluster ?? CreateCluster().ConfigureAwait(false).GetAwaiter().GetResult());
        private ILogger<SharedClusterWorker> Logger { get; }
        private ISharedSetupRootActors? SetupRootActors { get; }
        private IClusterSettings ClusterSettings { get; }
        private IMainWorker? MainWorker { get; }
        private IDescriptorProvider DescriptorProvider { get; }
        private ISharedClusterProviderFactory ClusterProviderFactory { get; }
        private IHostApplicationLifetime ApplicationLifetime { get; }
        //private readonly ISubscriptionFactory subscriptionFactory;
        //private IMetricsProvider? MetricsProvider { get; }
        private SharedClusterWorkerOptions ClusterOptions { get; }

        public SharedClusterWorker(
            ILogger<SharedClusterWorker> logger,
            IClusterSettings clusterSettings,
            IDescriptorProvider descriptorProvider,
            ISharedClusterProviderFactory clusterProviderFactory,
            IHostApplicationLifetime applicationLifetime,
            IOptions<SharedClusterWorkerOptions> clusterOptionsAccessor,
            ISharedSetupRootActors? setupRootActors = default,
            //ISubscriptionFactory subscriptionFactory = default,
            //IMetricsProvider? metricsProvider = default,
            IMainWorker? mainWorker = default
        )
        {
            Logger = logger;
            SetupRootActors = setupRootActors;
            ClusterSettings = clusterSettings;
            MainWorker = mainWorker;
            DescriptorProvider = descriptorProvider;
            ClusterProviderFactory = clusterProviderFactory;
            ApplicationLifetime = applicationLifetime;
            //this.subscriptionFactory = subscriptionFactory;
            //MetricsProvider = metricsProvider;
            ClusterOptions = clusterOptionsAccessor.Value;
        }

        public async Task<bool> Run()
        {
            Logger.LogInformation("Executing method: {MethodName}", $"{nameof(SharedClusterWorker)}.{nameof(Run)}");

            try
            {
                _cluster = await CreateCluster().ConfigureAwait(false);

                if (MainWorker == null)
                {
                    Logger.LogInformation("Main worker is null in: {MethodName}", $"{nameof(SharedClusterWorker)}.{nameof(Run)}");
                }
                else
                {
                    Logger.LogInformation("Trying to reach main worker in: {MethodName}", $"{nameof(SharedClusterWorker)}.{nameof(Run)}");
                    _ = Task.Run(() => MainWorker.Run(_cluster), ApplicationLifetime.ApplicationStopping);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Method failed: {MethodName}", $"{nameof(SharedClusterWorker)}.{nameof(Run)}");
            }

            return true;
        }

        public async Task<Cluster> CreateCluster()
        {
            try
            {
                var actorSystemConfig = ActorSystemConfig.Setup();

                //if (MetricsProvider != null)
                //{
                //    actorSystemConfig = actorSystemConfig
                //        .WithMetricsProviders(MetricsProvider);
                //}

                var system = new ActorSystem(actorSystemConfig);
                Logger.LogInformation("Setting up Cluster");
                Logger.LogInformation("ClusterName: " + ClusterSettings.ClusterName);
                Logger.LogInformation("PIDDatabaseName: " + ClusterSettings.PIDDatabaseName);
                Logger.LogInformation("PIDCollectionName: " + ClusterSettings.PIDCollectionName);

                var clusterProvider = ClusterProviderFactory.CreateClusterProvider(Logger);

                var identity = MongoIdentityLookup.GetIdentityLookup(ClusterSettings.ClusterName, ClusterSettings.PIDConnectionString, ClusterSettings.PIDCollectionName, ClusterSettings.PIDDatabaseName);

                var (clusterConfig, remoteConfig) = GenericClusterConfig.CreateClusterConfig(ClusterSettings, clusterProvider, identity, DescriptorProvider, Logger);

                if (SetupRootActors != null)
                {
                    clusterConfig = SetupRootActors.AddRootActors(clusterConfig);
                }

                Logger.LogInformation("Executing method: {MethodName} {Code}", $"{nameof(SharedClusterWorker)}.{nameof(CreateCluster)}", nameof(GrpcCoreRemote));
                _ = new GrpcCoreRemote(system, remoteConfig);

                Logger.LogInformation("Executing method: {MethodName} {Code}", $"{nameof(SharedClusterWorker)}.{nameof(CreateCluster)}", nameof(Cluster));
                _cluster = new Cluster(system, clusterConfig);

                Logger.LogInformation("Executing method: {MethodName} {Code}", $"{nameof(SharedClusterWorker)}.{nameof(CreateCluster)}", "StartMemberAsync");
                await _cluster.StartMemberAsync().ConfigureAwait(false);

                //if (this.subscriptionFactory != null)
                //{
                //    logger.LogInformation("Fire up subscriptions for system {id} {address}", system.Id, system.Address);
                //    await this.subscriptionFactory.FireUp(system).ConfigureAwait(false);
                //}
                //Logger.LogInformation("Executing method: {MethodName} {Code}", $"{nameof(SharedClusterWorker)}.{nameof(CreateCluster)}", nameof(SafeTask));
                _ = Task.Run(ConnectedLoop, ApplicationLifetime.ApplicationStopping);
                Logger.LogInformation("Finishing method: {MethodName}", $"{nameof(SharedClusterWorker)}.{nameof(CreateCluster)}");
                return _cluster;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SharedClusterWork failed");
                throw;
            }
        }

        public async Task Shutdown()
        {
            if (_cluster != null)
            {
                await _cluster.ShutdownAsync(true).ConfigureAwait(false);
                await Task.Delay(3000);
            }
        }

        private async Task ConnectedLoop()
        {
            //await Task.Yield();

            try
            {
                var counter = 0;
                while (!ApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    var members = _cluster?.MemberList.GetAllMembers();
                    var clusterKinds = _cluster?.GetClusterKinds();

                    if (clusterKinds == null || clusterKinds.Length == 0)
                    {
                        Logger.LogWarning("[SharedClusterWorker] clusterKinds {clusterKinds}", clusterKinds?.Length ?? 0);
                        Logger.LogWarning("[SharedClusterWorker] Restarting");
                        if (ClusterOptions.RestartOnFail)
                        {
                            _ = RestartMe();
                            break;
                        }

                    }

                    Connected = members?.Length > 0;
                    if (!Connected)
                    {
                        counter = 0;
                        Logger.LogInformation("[SharedClusterWorker] Connected {Connected}", Connected);
                    }

                    if (Connected)
                    {
                        if (counter % 20 == 0)
                        {
                            Logger.LogDebug("[SharedClusterWorker] Members {@Members}", members.Select(m => m.ToLogString()));
                            counter = 0;
                        }
                        counter++;
                    }

                    await Task.Delay(500);
                }
            }
            catch
            {
                // ignored
            }
        }

        public bool Connected { get; set; }

        private Task RestartMe()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                try { await Shutdown(); }
                catch
                {
                    // ignored
                }

                _cluster = null;
                await Task.Delay(5000);
                await Run();
            });

            return Task.CompletedTask;
        }
    }
}
