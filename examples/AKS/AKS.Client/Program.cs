using AKS.Client;
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

var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        //main dependencies
        services.AddProtoClient(configuration).AddDescriptorProvider<DescriptorProvider>();
        // this service is main starting point; add executing code there
        services.AddHostedService<MainWorker>();
    })
    .UseSerilog()
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => {
    Console.WriteLine("Canceling...");
    cts.Cancel();
    e.Cancel = true;
};

await host.RunAsync(cts.Token);
