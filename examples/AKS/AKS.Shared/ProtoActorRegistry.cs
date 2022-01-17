using System.Linq;
using AKS.Shared.Settings;
using AKS.Shared.Shared;
using AKS.Shared.Shared.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
//using Ubiquitous.Metrics;

namespace AKS.Shared
{
    public static class ProtoActorRegistry
    {
        public static ProtoActorClusterServices AddProtoCluster(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddOptions();
			ConfigureClusterSettings(services, configuration);

			if (configuration.GetChildren().Any(s => s.Key.Equals(SharedClusterWorkerOptions.Key)))
			{
				services.Configure<SharedClusterWorkerOptions>(configuration.GetSection(SharedClusterWorkerOptions.Key));
			}
			else
			{
				services.Configure<SharedClusterWorkerOptions>(_ => {});
			}

			if (configuration.GetChildren().Any(s => s.Key.Equals(KubernetesClusterOptions.Key)))
			{
				services.Configure<KubernetesClusterOptions>(configuration.GetSection(KubernetesClusterOptions.Key));
			}
			else
			{
				services.Configure<KubernetesClusterOptions>(_ => { });
			}

			services.AddSingleton<ISharedClusterProviderFactory, SharedClusterProviderFactory>();
			services.AddTransient<ITokenFactory, TokenFactory>();
			services.AddSingleton<SharedClusterWorker>();
			services.AddHostedService<ProtoActorClusterHostedService>();
			
			return new ProtoActorClusterServices(services);
		}

        public static ProtoActorClientServices AddProtoClient(this IServiceCollection services, IConfiguration configuration)
        {
	        services.AddOptions();
			ConfigureClusterSettings(services, configuration);

			if (configuration.GetChildren().Any(s => s.Key.Equals(SharedClusterClientOptions.Key)))
			{
				services.Configure<SharedClusterClientOptions>(configuration.GetSection(SharedClusterClientOptions.Key));
			}
			else
			{
				services.Configure<SharedClusterWorkerOptions>(_ => { });
			}

			if (configuration.GetChildren().Any(s => s.Key.Equals(KubernetesClusterOptions.Key)))
			{
				services.Configure<KubernetesClusterOptions>(configuration.GetSection(KubernetesClusterOptions.Key));
			}
			else
			{
				services.Configure<KubernetesClusterOptions>(_ => { });
			}


			services.AddSingleton<ISharedClusterProviderFactory, SharedClusterProviderFactory>();
	        services.AddTransient<ITokenFactory, TokenFactory>();
	        services.AddSingleton<ISharedClusterClient, SharedClusterClient>();
	        services.AddHostedService<ProtoActorClientHostedService>();

			return new ProtoActorClientServices(services);
        }

        private static void ConfigureClusterSettings(IServiceCollection services, IConfiguration configuration)
        {
	        services.Configure<ClusterSettings>(configuration.GetSection("ClusterSettings"));
	        services.AddSingleton<IClusterSettings>(sp =>
	        {
		        var firstSetting = sp.GetRequiredService<IOptions<ClusterSettings>>();
		        return firstSetting.Value;
	        });
        }
    }

    public class ProtoActorClusterServices
    {
	    private IServiceCollection Services { get; }

	    public ProtoActorClusterServices(IServiceCollection services)
	    {
		    Services = services;
	    }

		public ProtoActorClusterServices AddRootActors<TRootActors>() where TRootActors: class, ISharedSetupRootActors
		{
			Services.AddTransient<ISharedSetupRootActors, TRootActors>();
			return this;
		}

		public ProtoActorClusterServices AddDescriptorProvider<TProvider>() where TProvider : class, IDescriptorProvider
		{
			Services.AddTransient<IDescriptorProvider, TProvider>();
			return this;
		}

		public ProtoActorClusterServices AddMainWorker<TMainWorker>() where TMainWorker : class, IMainWorker
		{
			Services.AddTransient<IMainWorker, TMainWorker>();
			return this;
		}

		//public ProtoActorClusterServices AddProtoMetrics(params IMetricsProvider[] metricsProviders)
		//{
		//	if (metricsProviders == null)
		//	{
		//		return this;
		//	}
		//	foreach (IMetricsProvider metricsProvider in metricsProviders)
		//	{
		//		Services.AddSingleton(metricsProvider);
		//	}
			
		//	return this;
		//}
	}

    public class ProtoActorClientServices
    {
        private IServiceCollection Services { get; }

	    public ProtoActorClientServices(IServiceCollection services)
	    {
		    Services = services;
	    }

	    public ProtoActorClientServices AddDescriptorProvider<TProvider>() where TProvider : class, IDescriptorProvider
	    {
		    Services.AddTransient<IDescriptorProvider, TProvider>();
		    return this;
	    }

	    //public ProtoActorClientServices AddProtoMetrics(params IMetricsProvider[] metricsProviders)
	    //{
		   // if (metricsProviders == null)
		   // {
			  //  return this;
		   // }
		   // foreach (IMetricsProvider metricsProvider in metricsProviders)
		   // {
			  //  Services.AddSingleton<IMetricsProvider>(metricsProvider);
		   // }

		   // return this;
	    //}
	}
}
