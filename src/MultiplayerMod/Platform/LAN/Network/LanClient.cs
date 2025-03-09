using System;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using LiteNetLib;
using LiteNetLib.Utils;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.LAN.Network.Components;
using MultiplayerMod.Platform.LAN.Network.Messaging;
using UnityEngine;

namespace MultiplayerMod.Platform.LAN.Network;

[Dependency, UsedImplicitly]
[DependencyPriority(100)]
public class LanClient : IMultiplayerClient {
    public IMultiplayerClientId Id { get; }
    public MultiplayerClientState State { get; private set; } = MultiplayerClientState.Disconnected;

    public event Action<MultiplayerClientState>? StateChanged;
    public event Action<IMultiplayerCommand>? CommandReceived;

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanClient>();
    private readonly NetManager netManager;
    private readonly EventBasedNetListener listener;
    private readonly LanNetworkMessageSerializer serializer;

    private GameObject? gameObject;
    private NetPeer? serverConnection;

    public LanClient(LanNetworkMessageSerializer serializer) {
        this.serializer = serializer;
        Id = new LanMultiplayerClientId(Guid.NewGuid());

        listener = new EventBasedNetListener();
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
        listener.NetworkReceiveEvent += OnNetworkReceiveEvent;
        listener.NetworkErrorEvent += OnNetworkErrorEvent;

        var netConfig = new NetManager(listener) {
            AutoRecycle = true,
            DisconnectTimeout = 5000
        };

        netManager = netConfig;
    }

    public void Connect(IMultiplayerEndpoint endpoint) {
        if (State != MultiplayerClientState.Disconnected)
            throw new InvalidOperationException("Client is already connected or connecting");

        SetState(MultiplayerClientState.Connecting);

        if (!(endpoint is LanServerEndpoint lanEndpoint))
            throw new ArgumentException("Endpoint must be a LanServerEndpoint", nameof(endpoint));

        log.Info($"Connecting to LAN server at {lanEndpoint.EndPoint}");

        if (!netManager.IsRunning)
            netManager.Start();

        // Send our client ID in connection data
        var connectData = new NetDataWriter();
        connectData.Put(Id.ToString());

        serverConnection = netManager.Connect(lanEndpoint.EndPoint, connectData);

        // Create GameObject for updates
        gameObject = UnityObject.CreateStaticWithComponent<LanClientComponent>();
    }

    public void Disconnect() {
        if (State == MultiplayerClientState.Disconnected)
            return;

        log.Info("Disconnecting from LAN server");

        if (serverConnection != null)
            netManager.DisconnectPeer(serverConnection);

        CleanupConnection();
    }

    public void Tick() {
        netManager.PollEvents();
    }

    public void OnDestroy() {
        if (netManager.IsRunning) {
            netManager.Stop();
        }
        CleanupConnection();
    }

    public void Send(IMultiplayerCommand command, MultiplayerCommandOptions options = MultiplayerCommandOptions.None) {
        if (State != MultiplayerClientState.Connected || serverConnection == null)
            throw new InvalidOperationException("Cannot send command: client is not connected");

        try {
            var data = serializer.Serialize(command, options);
            serverConnection.Send(data, DeliveryMethod.ReliableOrdered);
        } catch (Exception ex) {
            log.Error($"Error sending command {command.GetType().Name}: {ex.Message}");
            SetState(MultiplayerClientState.Error);
        }
    }

    private void SetState(MultiplayerClientState newState) {
        if (State == newState) return;

        State = newState;
        StateChanged?.Invoke(newState);
    }

    private void OnPeerConnected(NetPeer peer) {
        log.Info($"Connected to server {peer.Id}");
        serverConnection = peer;
        SetState(MultiplayerClientState.Connected);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        log.Info($"Disconnected from server. Reason: {disconnectInfo.Reason}");
        CleanupConnection();
    }

    private void OnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) {
        try {
            var command = serializer.Deserialize(reader);
            if (command != null) {
                CommandReceived?.Invoke(command);
            }
        } catch (Exception ex) {
            log.Error($"Error deserializing command: {ex.Message}");
        }
    }

    private void OnNetworkErrorEvent(IPEndPoint endPoint, SocketError socketError) {
        log.Error($"Network error: {socketError}");
        if (State != MultiplayerClientState.Disconnected) {
            SetState(MultiplayerClientState.Error);
        }
    }

    private void CleanupConnection() {
        serverConnection = null;

        if (gameObject != null) {
            UnityObject.Destroy(gameObject);
            gameObject = null;
        }

        SetState(MultiplayerClientState.Disconnected);
    }
}
