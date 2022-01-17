using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AKS.Shared
{
    public class ProtoActorClientHostedService : IHostedService
    {
	    private ILogger<ProtoActorClientHostedService> Logger { get; }
	    private ISharedClusterClient SharedClusterClient { get; }
        private IConfiguration Configuration { get; }
        private IHostApplicationLifetime ApplicationLifetime { get; }

        public ProtoActorClientHostedService(ILogger<ProtoActorClientHostedService> logger, 
		    ISharedClusterClient sharedClusterClient, 
			IConfiguration configuration,
		    IHostApplicationLifetime applicationLifetime)
	    {
		    Logger = logger;
		    SharedClusterClient = sharedClusterClient;
		    Configuration = configuration;
		    ApplicationLifetime = applicationLifetime;
	    }
	    public async Task StartAsync(CancellationToken cancellationToken)
	    {
		    ApplicationLifetime.ApplicationStarted.Register(OnStarted);
		    ApplicationLifetime.ApplicationStopping.Register(OnStopping);
		    ApplicationLifetime.ApplicationStopped.Register(OnStopped);

		    await SharedClusterClient.Startup();
	    }

	    public async Task StopAsync(CancellationToken cancellationToken)
	    {
		    await this.SharedClusterClient.Shutdown();
	    }

	    private void OnStopped()
	    {
		    Logger.LogInformation("OnStopped has been called.");
	    }

	    private void OnStopping()
	    {
		    Logger.LogInformation("SIGTERM received, waiting for 30 seconds");
			if (this.Configuration.GetChildren().Any(c => c.Key.StartsWith("Kubernetes", StringComparison.OrdinalIgnoreCase)))
			{
				Thread.Sleep(30_000);
			}
			Logger.LogInformation("Termination delay complete, continuing stopping process");
	    }

	    private void OnStarted()
	    {
		    Logger.LogInformation("OnStarted has been called.");
	    }
	}
}
