using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace AKS.Shared
{

    public class GenericActor
    {
        private ILogger Logger { get; }

        protected PIDValues PidValues { get; set; } = new();
        protected Props? ChildFactory { get; set; }
        private Dictionary<string, PID> Actors { get; } = new();

        public GenericActor(ILogger logger) => Logger = logger;

        public virtual Task ReceiveAsync(IContext context) =>
           context.Message switch
           {
               ClusterInit _ => Task.CompletedTask,
               //Stopping _ => Stopping(context),
               //Stopped _ => Stopped(context),
               Restarting _ => Task.CompletedTask,
               _ => throw new NotImplementedException($"Not implemenented for {context.Message?.GetType()}"),
               //ReceiveTimeout _ => ReceiveTimeout(context),
               //_ => UnknownCmd(context, context.Message)
           };


        //private string CreateKey(string tenant, string name, string eid)
        //{
        //    return $"@{tenant}:@{name}:@{eid}";
        //}

        //protected PID GetActor(IContext context, string tenant, string name, string eid, string objectId)
        //{
        //    var key = CreateKey(tenant, name, eid);
        //    if (!Actors.ContainsKey(key))
        //    {
        //        var newChildActor = context.Spawn(ChildFactory);
        //        Actors.Add(key, newChildActor);
        //        context.Send(newChildActor, new LoadStateCmd(tenant, name, eid, objectId));
        //    }
        //    return Actors[key];
        //}

        //protected PID GetQueryActor(IContext context, string tenant, string name, string queryStringBson)
        //{
        //    if (queryStringBson == null)
        //    {
        //        throw new ArgumentNullException(nameof(queryStringBson));
        //    }
        //    var hash = queryStringBson.GetHashCode(StringComparison.Ordinal);
        //    var key = $"/query/{tenant}/{name}/{hash}";
        //    if (!Actors.ContainsKey(key))
        //    {
        //        var newChildActor = context.Spawn(ChildFactory);
        //        Actors.Add(key, newChildActor);
        //    }
        //    return Actors[key];
        //}

#pragma warning disable IDE0060
        // the methods above are part of API, warning disable

        //protected Task Stopped(IContext context)
        //{
        //    return Task.CompletedTask;
        //}

        //protected Task Stopping(IContext context)
        //{
        //    return Task.CompletedTask;
        //}
        //protected Task Restarting(IContext context)
        //{
        //    return Task.CompletedTask;
        //}

        //protected Task ReceiveTimeout(IContext context)
        //{
        //    try
        //    {
        //        Logger?.LogInformation(
        //            "[GenericActor] ReceiveTimeout Stopping. Actor: {ActorName}, Eid: {Eid}",
        //            GetType().Name,
        //            PidValues?.Eid);

        //        context.Self.Stop(context.System);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger?.LogError(
        //            e,
        //            "[GenericActor] ReceiveTimeout failed. Actor: {ActorName}, Eid: {Eid}",
        //            GetType().Name, 
        //            PidValues?.Eid);
        //    }
        //    return Task.CompletedTask;
        //}

        //protected Task UnknownCmd(IContext context, object? cmd)
        //{
        //    return Task.CompletedTask;
        //}

        //protected Task GarbageCollect(IContext context, GarbageCollectCmd cmd)
        //{
        //    return Task.CompletedTask;
        //}
#pragma warning restore IDE0060

        protected virtual Task Started(IContext context)
        {
            PidValues = context.Self.ExtractIdValues();

            Logger?.LogInformation("{ActorName} - Started. Eid {Eid}", GetType().Name, PidValues.Eid);

            //context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
            return Task.CompletedTask;
        }

        //protected bool GetParams(IContext context, out string? p1)
        //{
        //    string[] cmdParts = context.Self.Id.Split("$");
        //    if (cmdParts.Length > 0)
        //    {
        //        string[] parts = cmdParts[0].Split('/');

        //        if (parts.Length > 1)
        //        {
        //            p1 = parts[parts.Length-1];
        //            return true;
        //        }
        //    }
        //    p1 = null;
        //    return false;
        //}

        //protected bool GetParams(IContext context, out string? p1, out string? p2)
        //{
        //    string[] cmdParts = context.Self.Id.Split("$");
        //    if (cmdParts.Length > 0)
        //    {
        //        string[] parts = cmdParts[0].Split('/');

        //        if (parts.Length > 2)
        //        {
        //            p1 = parts[parts.Length - 2];
        //            p2 = parts[parts.Length - 1];
        //            return true;
        //        }
        //    }
        //    p1 = null;
        //    p2 = null;
        //    return false;
        //}
    }
}
