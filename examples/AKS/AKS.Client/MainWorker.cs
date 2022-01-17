using System.Net;
using System.Net.Mail;
using aks.messages;
using AKS.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AKS.Client;

internal class MainWorker : BackgroundService
{
    private ISharedClusterClient ClusterClient { get; }
    private ILogger<MainWorker> Logger { get; }
    private string ServerClusterKind => "main";
    private string ServerActorPath => "a/b";
    private string ServerId { get; set; } = "";
    private int MessagesReceivedByServer { get; set; }
    private int MessagesSentToServer { get; set; }
    private int FailedPingAttemptsCount { get; set; }
    private int PingAttemptsLimit { get; set; } = 40;
    private bool NotificationEmailWasSent { get; set; }

    public MainWorker(ISharedClusterClient clusterClient, ILogger<MainWorker> logger)
    {
        ClusterClient = clusterClient;
        Logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Execute(stoppingToken);

    private async Task Execute(CancellationToken stoppingToken)
    {
        var methodName = $"{nameof(MainWorker)}.{nameof(Execute)}";
        Logger.LogInformation("Executing method {MethodName}", methodName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // for testing to work in cluster a cycle consists of:
                // 1. few ping server commands
                // 2. then sending "Shutdown" command to the server
                // if server is not available after long period of time, then send an email to pay attention
                for (var i = 1; i <= 30; i++)
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        var serverAvailable = await PingServer(i);
                        if (serverAvailable)
                        {
                            FailedPingAttemptsCount = 0;
                            NotificationEmailWasSent = false;
                        }
                        else
                        {
                            FailedPingAttemptsCount++;
                        }
                        Thread.Sleep(2000);
                    }
                }

                if (FailedPingAttemptsCount > PingAttemptsLimit && !NotificationEmailWasSent)
                {
                    NotificationEmailWasSent = true;
                    await SendNotificationEmail();
                }

                // commented for the future test cases
                //await SendShutdownServerCommand();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to execute method {MethodName}", methodName);
            }

            Thread.Sleep(2000);
        }
    }

    private async Task<bool> PingServer(int attemptNumber)
    {
        var correlationId = Guid.NewGuid().ToString();

        Logger.LogInformation(
            "Executing {MethodName}: ActorPath {ActorPath}, ClusterKind {ClusterKind}. CorrelationId: {CorrelationId}, Attempt #{AttemptNumber}",
            $"{nameof(MainWorker)}.{nameof(PingServer)}",
            ServerActorPath,
            ServerClusterKind,
            correlationId,
            attemptNumber);

        MessagesSentToServer++;

        // increase server delay every new attempt
        var cmd = new GetServerInfo()
        {
            CorrelationId = correlationId,
            WithDelayMs = attemptNumber * 1000
        };

        // every few times raise server exception
        if (attemptNumber % 10 == 0)
        {
            cmd.RaiseException = true;
        }

        var response = await ClusterClient.RequestAsync<ServerInfo>(
            ServerActorPath,
            ServerClusterKind,
            cmd,
            new CancellationTokenSource(15000).Token);

        if (response == null)
        {
            Logger.LogWarning("Executed {MethodName}: Cancelled on timeout", $"{nameof(MainWorker)}.{nameof(PingServer)}");
            return false;
        }

        if (ServerId == "")
        {
            // connected first time from this client
            Logger.LogInformation("Connected to server {ServerId}", response.ServerId);
        }
        else if (ServerId != response.ServerId)
        {
            // connected to a different server
            Logger.LogWarning("Connected to NEW server {ServerId}. Previos server {LastServerId}", response.ServerId, ServerId);
        }

        ServerId = response.ServerId;
        MessagesReceivedByServer = response.MessagesReceived;

        Logger.LogInformation(
            "Messages total sent: {MessagesSent}. Messages received by current server: {MessagesReceived}",
            MessagesSentToServer,
            MessagesReceivedByServer);

        return true;
    }

    private async Task SendShutdownServerCommand()
    {
        Logger.LogInformation(
            "Executing {MethodName}: ActorPath {ActorPath}, ClusterKind {ClusterKind}",
            $"{nameof(MainWorker)}.{nameof(SendShutdownServerCommand)}",
            ServerActorPath,
            ServerClusterKind);

        var response = await ClusterClient.RequestAsync<ServerInfo>(
            ServerActorPath,
            ServerClusterKind,
            new ShutdownServerInstance(),
            new CancellationTokenSource(5000).Token);

        if (response == null)
        {
            Logger.LogWarning("Executed {MethodName}: Cancelled on timeout", $"{nameof(MainWorker)}.{nameof(SendShutdownServerCommand)}");
            return;
        }

        Logger.LogWarning("Executed {MethodName}: Success", $"{nameof(MainWorker)}.{nameof(SendShutdownServerCommand)}");
    }

    private async Task SendNotificationEmail()
    {
        // set api key to send notification
        var apiKey = "";
        var sendEmail = apiKey != "";
        if (!sendEmail)
        {
            return;
        }

        try
        {
            var body = new StringBuilder();
            body.Append("<p>Dear colleague,</p>");
            body.Append($"<p>The AKS client cannot reach AKS server for a long time. Please check logs for details with 'kubectl logs' command<br />");

            using var client = new SmtpClient("smtp.sendgrid.net", 587)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("apikey", apiKey)
            };

            var from = new MailAddress("noreply@thedamproject.com", "DAM notifier", Encoding.UTF8);
            var to = new MailAddress("petro.vdovukhin@tietoevry.com");

            using var message = new MailMessage(from, to)
            {
                Body = body.ToString(),
                BodyEncoding = Encoding.UTF8,
                Subject = "AKS client could not reach AKS server",
                SubjectEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send an email with notification");
        }
    }
}
