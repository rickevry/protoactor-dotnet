using System;
using Microsoft.Extensions.Logging;
using Google.Protobuf.Reflection;
using Proto.Cluster;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using AKS.Shared;
using Proto.Cluster.Identity;
using System.Collections.Generic;
using Grpc.Core;

namespace AKS.Shared
{
    public class GenericClusterConfig
    {
        public static (ClusterConfig, GrpcCoreRemoteConfig) CreateClusterConfig(IClusterSettings clusterSettings, IClusterProvider clusterProvider, IIdentityLookup identityLookup, IDescriptorProvider descriptorProvider, ILogger logger)
        {
            var clusterName = clusterSettings.ClusterName;

            var hostip = Environment.GetEnvironmentVariable("PROTOHOST");
            var host = clusterSettings.ClusterHost;
            var advertisedHost = clusterSettings.ClusterHost;
            var port = clusterSettings.ClusterPort;

            if ("protohost".Equals(host))
            {
                host = hostip;
                advertisedHost = hostip;
                logger.LogDebug($"Using PROTOHOST");

            }
            logger.LogDebug($"BindTo to {host} port {port}");
            logger.LogDebug($"WithAdvertisedHost to {advertisedHost}");

            FileDescriptor[] descriptors = descriptorProvider.GetDescriptors();


            // TOOD: This doesn't seem to work. Why?
            List<ChannelOption> options = new List<ChannelOption>()
            {

                new ChannelOption(ChannelOptions.MaxSendMessageLength, (100 * 1024 * 1024)),
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, (100 * 1024 * 1024))
            };


            GrpcCoreRemoteConfig remoteConfig = GrpcCoreRemoteConfig.BindTo(host, port)
                .WithEndpointWriterMaxRetries(3)
                .WithAdvertisedHost(advertisedHost)
                .WithProtoMessages(descriptors)
                .WithChannelOptions(options);

            var clusterConfig = ClusterConfig.Setup(clusterName, clusterProvider, identityLookup);
            return (clusterConfig, remoteConfig);

        }
    }
}
