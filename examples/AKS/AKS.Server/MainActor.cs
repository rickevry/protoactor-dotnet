using aks.messages;
using AKS.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;

namespace AKS.Server
{
    [Actor("main")]
    internal class MainActor : GenericActor, IActor
    {
        private IHostApplicationLifetime ApplicationLifetime { get; }
        private ILogger<MainActor> Logger { get; }
        private static Guid ServerId { get; } = Guid.NewGuid();
        private static int MessagesReceived { get; set; } = 0;

        public MainActor(IHostApplicationLifetime appLifetime, ILogger<MainActor> logger) : base(logger)
        {
            ApplicationLifetime = appLifetime;
            Logger = logger;
        }

        public override Task ReceiveAsync(IContext context)
        {
            Logger.LogInformation("Executing method {MethodName}", $"{nameof(MainActor)}.{nameof(ReceiveAsync)}");
            var task = context.Message switch
            {
                HealthCheck _ => HandleGetServerInfo(context, new GetServerInfo(), false),
                GetServerInfo cmd => HandleGetServerInfo(context, cmd, true),
                ShutdownServerInstance _ => ExitApplication(context),
                Started _ => base.Started(context),
                _ => base.ReceiveAsync(context)
            };

            return task;
        }

        private Task HandleGetServerInfo(IContext context, GetServerInfo cmd, bool increaseMessageCount)
        {
            Logger.LogInformation(
                "Executing method {MethodName}. CorrelationId: {CorrelationId}",
                $"{nameof(MainActor)}.{nameof(HandleGetServerInfo)}",
                cmd.CorrelationId);

            if (increaseMessageCount)
            {
                MessagesReceived++;
            }

            if (cmd.WithDelayMs > 0)
            {
                Logger.LogInformation(
                    "Executing method {MethodName}: Sleeping the thread for {Delay}. CorrelationId: {CorrelationId}",
                    $"{nameof(MainActor)}.{nameof(HandleGetServerInfo)}",
                    cmd.WithDelayMs,
                    cmd.CorrelationId);

                Thread.Sleep(cmd.WithDelayMs);
            }

            if (cmd.RaiseException)
            {
                Logger.LogInformation(
                    "Executing method {MethodName}. CorrelationId: {CorrelationId}. Raising an exception..",
                    $"{nameof(MainActor)}.{nameof(HandleGetServerInfo)}",
                    cmd.CorrelationId);
                throw new MainActorException("The client asked for raising this exception. Testing server recovering.");
            }

            context.Respond(new ServerInfo { ServerId = ServerId.ToString(), MessagesReceived = MessagesReceived });

            Logger.LogInformation(
                "Executed method {MethodName}. CorrelationId: {CorrelationId}",
                $"{nameof(MainActor)}.{nameof(HandleGetServerInfo)}",
                cmd.CorrelationId);

            return Task.CompletedTask;
        }

        private Task ExitApplication(IContext context)
        {
            Logger.LogInformation(
                "Executing method {MethodName}",
                $"{nameof(MainActor)}.{nameof(ExitApplication)}");

            context.Respond(new ServerInfo { ServerId = ServerId.ToString(), MessagesReceived = MessagesReceived });

            ApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }
    }

    internal class MainActorException: ApplicationException
    {
        public MainActorException() : base() { }

        public MainActorException(string? message) : base(message) { }
        
        public MainActorException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
