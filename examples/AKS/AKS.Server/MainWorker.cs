using aks.messages;
using AKS.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace AKS.Server
{
    public class MainWorker : IMainWorker
    {
        private ILogger<MainWorker> Logger { get; }
        private IHostApplicationLifetime AppLifetime { get; }

        public MainWorker(ILogger<MainWorker> logger, IHostApplicationLifetime appLifetime)
        {
            Logger = logger;
            AppLifetime = appLifetime;
            logger.LogInformation("Constructor has been started: {ObjectType}", typeof(MainWorker));
        }

        public Task Run(Cluster cluster)
        {
            Logger.LogInformation("Executing method: {MethodName}", $"{nameof(MainWorker)}.{nameof(Run)}");

            try
            {
                AppLifetime.ApplicationStarted.Register(() => Task.Run(() => RunLoop(cluster), cancellationToken: AppLifetime.ApplicationStopping));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Method failed: {MethodName}", $"{nameof(MainWorker)}.{nameof(Run)}");
            }
            
            return Task.CompletedTask;
        }

        private async Task RunLoop(Cluster cluster)
        {
            //await Task.Yield();
            //await Task.Delay(5000, AppLifetime.ApplicationStopping);

            // TEST on startup
            Logger.LogInformation("Performing health check after cluster has started...");
            var response = await cluster.RequestAsync<ServerInfo>(
                "a/b",
                "main",
                new HealthCheck(),
                new CancellationTokenSource(300_000).Token);

            if (response == null)
            {
                Logger.LogWarning("On healthcheck: Cancelled on timeout");
            }
            else
            {
                Logger.LogInformation("On healthcheck: Success");
            }

            var i = 0;
            while (!AppLifetime.ApplicationStopping.IsCancellationRequested)
            {
                await Task.Delay(1000);

                var timeout = 1000 * i++;

                if (timeout > 11_000)
                {
                    i = 0;
                }

                try
                {
                    var serverInfo = await cluster.RequestAsync<ServerInfo>(
                        "a/b",
                        "main",
                        new GetServerInfo { CorrelationId = i.ToString(), WithDelayMs = timeout},
                        new CancellationTokenSource(10_000).Token);

                    if (serverInfo == null)
                    {
                        Logger.LogWarning("On get server info healthcheck: Cancelled on timeout {Timeout} ms", timeout);
                    }
                    else
                    {
                        Logger.LogInformation("On get server info healthcheck: Success. Timeout {Timeout} ms", timeout);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "On get server info healthcheck: Failed to request cluster async. {Timeout} ms", timeout);
                }
            }

            Logger.LogInformation("Executed method {MethodName}: Cancellation has been requested", $"{nameof(MainWorker)}.{nameof(RunLoop)}");
        }
    }
}
