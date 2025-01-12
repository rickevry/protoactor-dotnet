// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public delegate (bool, T) ConsensusCheck<T>(GossipState state, IImmutableSet<string> memberIds, IContext context);

    public record GossipUpdate(string MemberId, string Key, Any Value, long SequenceNumber);

    public record GetGossipStateRequest(string Key);

    public record GetGossipStateResponse(ImmutableDictionary<string, Any> State);

    public record SetGossipStateKey(string Key, IMessage Value);

    public record SetGossipStateResponse;

    public record SendGossipStateRequest;

    public record SendGossipStateResponse;

    internal record AddConsensusCheck(ConsensusCheck Check);

    internal record RemoveConsensusCheck(string Id);

    public class Gossiper
    {
        public const string GossipActorName = "gossip";
        private readonly Cluster _cluster;
        private readonly RootContext _context;

        private static readonly ILogger Logger = Log.CreateLogger<Gossiper>();
        private PID _pid = null!;

        public Gossiper(Cluster cluster)
        {
            _cluster = cluster;
            _context = _cluster.System.Root;
        }

        public async Task<ImmutableDictionary<string, T>> GetState<T>(string key) where T : IMessage, new()
        {
            _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

            var res = await _context.RequestAsync<GetGossipStateResponse>(_pid, new GetGossipStateRequest(key));

            var dict = res.State;
            var typed = ImmutableDictionary<string, T>.Empty;

            foreach (var (k, value) in dict)
            {
                typed = typed.SetItem(k, value.Unpack<T>());
            }

            return typed;
        }

        // Send message to update member state
        // Will not wait for completed state update
        public void SetState(string key, IMessage value)
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
            _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

            if (_pid == null)
            {
                return;
            }

            _context.Send(_pid, new SetGossipStateKey(key, value));
        }

        public Task SetStateAsync(string key, IMessage value)
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
            _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

            if (_pid == null)
            {
                return Task.CompletedTask;
            }

            return _context.RequestAsync<SetGossipStateResponse>(_pid, new SetGossipStateKey(key, value));
        }

        internal Task StartAsync()
        {
            var props = Props.FromProducer(() => new GossipActor(_cluster.Config.GossipRequestTimeout));
            _pid = _context.SpawnNamed(props, GossipActorName);
            _cluster.System.EventStream.Subscribe<ClusterTopology>(topology => _context.Send(_pid, topology));
            Logger.LogInformation("Started Cluster Gossip");
            _ = SafeTask.Run(GossipLoop);

            return Task.CompletedTask;
        }

        private async Task GossipLoop()
        {
            Logger.LogInformation("Starting gossip loop");
            await Task.Yield();

            while (!_cluster.System.Shutdown.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay((int) _cluster.Config.GossipInterval.TotalMilliseconds);
                    SetState("heartbeat", new MemberHeartbeat());
                    await SendStateAsync();
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "Gossip loop failed");
                }
            }
        }

        public class ConsensusCheckBuilder<T>
        {
            private readonly ImmutableList<(string, Func<Any, T?>)> _getConsensusValues;

            private readonly Lazy<ConsensusCheck<T>> _check;
            public ConsensusCheck<T> Check => _check.Value;

            public string[] AffectedKeys => _getConsensusValues.Select(it => it.Item1).Distinct().ToArray();

            private ConsensusCheckBuilder(ImmutableList<(string, Func<Any, T?>)> getValues)
            {
                _getConsensusValues = getValues;
                _check = new Lazy<ConsensusCheck<T>>(Build);
            }

            public ConsensusCheckBuilder(string key, Func<Any, T?> getValue)
            {
                _getConsensusValues = ImmutableList.Create<(string, Func<Any, T?>)>((key, getValue));
                _check = new Lazy<ConsensusCheck<T>>(this.Build, LazyThreadSafetyMode.PublicationOnly);
            }

            public static ConsensusCheckBuilder<T> Create<TE>(string key, Func<TE, T?> getValue) where TE : IMessage, new()
                => new(key, MapFromAny(getValue));

            private static Func<Any, T?> MapFromAny<TE>(Func<TE, T?> getValue) where TE : IMessage, new()
                => any => any.TryUnpack<TE>(out var envelope) ? getValue(envelope) : default;

            public ConsensusCheckBuilder<T> InConsensusWith<TE>(string key, Func<TE, T> getValue) where TE : IMessage, new()
                => new(_getConsensusValues.Add((key, MapFromAny(getValue))));

            private static Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, T value)> MapToValue(
                (string, Func<Any, T?>) valueTuple
            )
            {
                var (key, unpack) = valueTuple;
                return (kv) => {
                    var (member, state) = kv;
                    var value = state.Values.TryGetValue(key, out var any) ? unpack(any.Value) : default;
                    return (member, key, value);
                };
            }

            private ConsensusCheck<T> Build()
            {
                if (_getConsensusValues.Count == 1)
                {
                    var mapToValue = MapToValue(_getConsensusValues.Single());
                    return (state, ids, context) => {
                        var memberStates = GetValidMemberStates(state, ids);

                        // Missing state, cannot have consensus
                        if (memberStates.Length < ids.Count)
                        {
                            return default;
                        }

                        var valueTuples = memberStates.Select(mapToValue);
                        // ReSharper disable PossibleMultipleEnumeration
                        var result = valueTuples.Select(it => it.value).HasConsensus();

                        if (context.System.Config.DeveloperSupervisionLogging)
                        {
                            Logger.LogDebug("{SystemId}, consensus {Consensus}: {Values}", context.System.Id, result.Item1, valueTuples
                                .GroupBy(it => (it.key, it.value), tuple => tuple.member).Select(
                                    grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                                (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                                )
                            );
                        }

                        return result;
                    };
                }

                var mappers = _getConsensusValues.Select(MapToValue).ToArray();

                return (state, ids, context) => {
                    var memberStates = GetValidMemberStates(state, ids);

                    if (memberStates.Length < ids.Count) // Not all members have state..
                    {
                        return default;
                    }

                    var valueTuples = memberStates
                        .SelectMany(memberState => mappers.Select(mapper => mapper(memberState)));
                    var consensus = valueTuples.Select(it => it.value).HasConsensus();

                    if (context.System.Config.DeveloperSupervisionLogging)
                    {
                        Logger.LogDebug("{SystemId}, consensus {Consensus}: {Values}", context.System.Id, consensus.Item1, valueTuples
                            .GroupBy(it => (it.key, it.value), tuple => tuple.member).Select(
                                grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                            (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                            )
                        );
                    }

                    // ReSharper enable PossibleMultipleEnumeration
                    return consensus;
                };

                KeyValuePair<string, GossipState.Types.GossipMemberState>[] GetValidMemberStates(GossipState state, IImmutableSet<string> ids)
                    => state.Members
                        .Where(member => ids.Contains(member.Key))
                        .Select(member => member).ToArray();
            }
        }

        public IConsensusHandle<TV> RegisterConsensusCheck<T, TV>(string key, Func<T, TV?> getValue) where T : IMessage, new()
            => RegisterConsensusCheck(ConsensusCheckBuilder<TV>.Create(key, getValue));

        public IConsensusHandle<T> RegisterConsensusCheck<T>(ConsensusCheckBuilder<T> builder)
            => RegisterConsensusCheck(builder.Check, builder.AffectedKeys);

        public IConsensusHandle<T> RegisterConsensusCheck<T>(ConsensusCheck<T> hasConsensus, string[] affectedKeys)
        {
            var id = Guid.NewGuid().ToString("N");
            var handle = new GossipConsensusHandle<T>(() => _context.Send(_pid, new RemoveConsensusCheck(id))
            );

            _context.Send(_pid, new AddConsensusCheck(new ConsensusCheck(id, CheckConsensus, affectedKeys)));

            return handle;

            void CheckConsensus(GossipState state, IImmutableSet<string> members, IContext context)
            {
                var (consensus, value) = hasConsensus(state, members, context);

                if (consensus)
                {
                    handle.TrySetConsensus(value!);
                }
                else
                {
                    handle.TryResetConsensus();
                }
            }
        }

        private async Task SendStateAsync()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (_pid == null)
            {
                //just make sure a cluster client cant send
                return;
            }

            try
            {
                await _context.RequestAsync<SendGossipStateResponse>(_pid, new SendGossipStateRequest(), CancellationTokens.FromSeconds(5));
            }
            catch (DeadLetterException)
            {
            }
            catch (OperationCanceledException)
            {
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                //TODO: log
            }
        }

        internal Task ShutdownAsync()
        {
            Logger.LogInformation("Shutting down heartbeat");
            _context.Stop(_pid);
            Logger.LogInformation("Shut down heartbeat");
            return Task.CompletedTask;
        }
    }
}