using AKS.Server;
using AKS.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = new ConfigurationBuilder();

builder.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", true, true)
    .AddEnvironmentVariables();

var configuration = builder.Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
    .CreateLogger();

Log.Logger.Information("{Configuration}", configuration.GetDebugView());

try
{
    var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) => {
        //main dependencies
        services.AddSingleton<MainActor>();

        services.AddProtoCluster(configuration)
                .AddMainWorker<MainWorker>()
                .AddDescriptorProvider<DescriptorProvider>()
                .AddRootActors<RootActorsSetup>();
    })
    .UseSerilog()
    .Build();

    Log.Logger.Information("The host has been built successfully. Starting...");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Logger.Error(ex, "Failed to run the host");
}