using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AKS.Shared
{
    public class ProtoActorClusterHostedService : IHostedService
    {
	    private ILogger<ProtoActorClusterHostedService> Logger { get; }
	    private SharedClusterWorker SharedClusterWorker { get; }
        private IConfiguration Configuration { get; }
        private IHostApplicationLifetime ApplicationLifetime { get; }

        public ProtoActorClusterHostedService(ILogger<ProtoActorClusterHostedService> logger, 
		    SharedClusterWorker sharedClusterWorker, 
			IConfiguration configuration,
		    IHostApplicationLifetime applicationLifetime)
	    {
		    Logger = logger;
		    SharedClusterWorker = sharedClusterWorker;
		    Configuration = configuration;
		    ApplicationLifetime = applicationLifetime;
	    }

	    public async Task StartAsync(CancellationToken cancellationToken)
	    {
		    ApplicationLifetime.ApplicationStarted.Register(OnStarted);
		    ApplicationLifetime.ApplicationStopping.Register(OnStopping);
		    ApplicationLifetime.ApplicationStopped.Register(OnStopped);

		    await SharedClusterWorker.Run();
	    }

	    public async Task StopAsync(CancellationToken cancellationToken)
	    {
			await SharedClusterWorker.Shutdown();
		}

	    private void OnStopped()
	    {
		    Logger.LogInformation("OnStopped has been called.");
	    }

	    private void OnStopping()
	    {
		    Logger.LogInformation("SIGTERM received, waiting for 10 seconds");
			
			if(Configuration.GetChildren().Any(c => c.Key.StartsWith("Kubernetes", StringComparison.OrdinalIgnoreCase)))
			{
				Thread.Sleep(10_000);
			}
		    
		    Logger.LogInformation("Termination delay complete, continuing stopping process");
	    }

	    private void OnStarted()
	    {
		    Logger.LogInformation("OnStarted has been called.");
	    }
	}
}
