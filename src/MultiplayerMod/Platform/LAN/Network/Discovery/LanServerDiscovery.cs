using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using LiteNetLib;
using LiteNetLib.Utils;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Logging;

namespace MultiplayerMod.Platform.LAN.Network.Discovery;

[Dependency, UsedImplicitly]
public class LanServerDiscovery {
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanServerDiscovery>();

    // Default discovery port
    public const int DefaultDiscoveryPort = 9050;

    // Time between discovery broadcasts in milliseconds
    private const int DiscoveryInterval = 1000;
    private long lastDiscoveryTime;

    // Discovery server and client
    private NetManager? discoveryClient;
    private EventBasedNetListener listener;

    // Server information
    private LanServerInfo? serverInfo;
    private readonly Dictionary<Guid, LanServerInfo> discoveredServers = new();

    // Event for server discovery
    public event Action<LanServerInfo>? ServerDiscovered;

    public LanServerDiscovery() {
        listener = new EventBasedNetListener();
        listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnected;
    }

    public void StartDiscoveryClient() {
        if (discoveryClient != null) {
            StopDiscovery();
        }

        log.Info("Starting LAN discovery client");
        discoveryClient = new NetManager(listener);
        discoveryClient.UnconnectedMessagesEnabled = true;
        discoveryClient.BroadcastReceiveEnabled = true;
        discoveryClient.Start();
        lastDiscoveryTime = 0;
    }

    public void StartDiscoveryServer(LanServerInfo info) {
        if (discoveryClient != null) {
            StopDiscovery();
        }

        serverInfo = info;
        log.Info($"Starting LAN discovery server with name: {info.Name} on port {info.Endpoint.Port}");

        discoveryClient = new NetManager(listener);
        discoveryClient.UnconnectedMessagesEnabled = true;
        discoveryClient.BroadcastReceiveEnabled = true;
        discoveryClient.Start();
    }

    public void StopDiscovery() {
        if (discoveryClient == null) return;

        log.Info("Stopping LAN discovery");
        discoveryClient.Stop();
        discoveryClient = null;
        discoveredServers.Clear();
        serverInfo = null;
    }

    public void Tick() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (discoveryClient == null || !discoveryClient.IsRunning) return;

        discoveryClient.PollEvents();

        // If we're in server mode, respond to discovery requests
        if (serverInfo != null) {
            // log.Info("Server mode, sending server info");
            return;
        };

        // If we're in client mode and it's time for a discovery broadcast
        if (now - lastDiscoveryTime >= DiscoveryInterval) {
            SendDiscoveryRequest();
            lastDiscoveryTime = now;
        }
    }

    private void SendDiscoveryRequest() {
        if (discoveryClient == null || !discoveryClient.IsRunning) return;

        var writer = new NetDataWriter();
        writer.Put("LANDISC"); // Discovery identifier

        // log.Info("Sending LAN discovery request");
        discoveryClient.SendBroadcast(writer, DefaultDiscoveryPort);
    }

    private void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
        // log.Info($"Received LAN discovery message from {remoteEndPoint}. Type: {messageType}");
        if (discoveryClient == null || !discoveryClient.IsRunning) return;

        try {
            // Handle discovery requests (server mode)
            if (serverInfo != null && messageType == UnconnectedMessageType.Broadcast) {
                var message = reader.GetString();
                if (message == "LANDISC") {
                    log.Trace($"Discovery request received from {remoteEndPoint}, sending server info");
                    var response = new NetDataWriter();
                    response.Put("LANINFO"); // Server info response identifier
                    serverInfo.Serialize(response);
                    discoveryClient.SendUnconnectedMessage(response, remoteEndPoint);
                }
                return;
            }

            // Handle server responses (client mode)
            if (messageType == UnconnectedMessageType.BasicMessage) {
                var message = reader.GetString();
                if (message == "LANINFO") {
                    var info = new LanServerInfo();
                    info.Deserialize(reader);

                    // Only notify if this is a new server or an updated one
                    if (!discoveredServers.TryGetValue(info.ServerId, out var existingInfo) ||
                        existingInfo.Name != info.Name) {
                        log.Info($"Discovered LAN server: {info.Name} at {info.Endpoint}");
                        discoveredServers[info.ServerId] = info;
                        ServerDiscovered?.Invoke(info);
                    }
                }
            }
        } catch (Exception ex) {
            log.Error($"Error processing discovery message: {ex}");
        }
    }
}
