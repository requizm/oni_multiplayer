using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using LiteNetLib;
using LiteNetLib.Utils;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Multiplayer.Commands.Registry;
using MultiplayerMod.Multiplayer.Players;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.LAN.Network.Components;
using MultiplayerMod.Platform.LAN.Network.Discovery;
using MultiplayerMod.Platform.LAN.Network.Messaging;
using UnityEngine;

namespace MultiplayerMod.Platform.LAN.Network;

[Dependency, UsedImplicitly]
[DependencyPriority(100)]
public class LanServer : IMultiplayerServer {
    public const int DefaultPort = 9051;

    public MultiplayerServerState State { get; private set; } = MultiplayerServerState.Stopped;
    public IMultiplayerEndpoint Endpoint => new LanServerEndpoint(new IPEndPoint(GetLocalIPAddress(), port));
    public List<IMultiplayerClientId> Clients => new(clients.Keys);

    public event Action<MultiplayerServerState>? StateChanged;
    public event Action<IMultiplayerClientId>? ClientConnected;
    public event Action<IMultiplayerClientId>? ClientDisconnected;
    public event Action<IMultiplayerClientId, IMultiplayerCommand>? CommandReceived;

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanServer>();
    private readonly Dictionary<IMultiplayerClientId, NetPeer> clients = new();
    private readonly NetManager netManager;
    private readonly EventBasedNetListener listener;
    private readonly LanNetworkMessageSerializer serializer;
    private readonly MultiplayerCommandRegistry commandRegistry;
    private readonly LanServerDiscovery discovery;
    private readonly IPlayerProfileProvider playerProfileProvider;

    private GameObject? gameObject;
    private int port = DefaultPort;
    private readonly Guid serverId = Guid.NewGuid();

    public LanServer(
        LanNetworkMessageSerializer serializer,
        MultiplayerCommandRegistry commandRegistry,
        LanServerDiscovery discovery,
        IPlayerProfileProvider playerProfileProvider
    ) {
        this.serializer = serializer;
        this.commandRegistry = commandRegistry;
        this.discovery = discovery;
        this.playerProfileProvider = playerProfileProvider;

        listener = new EventBasedNetListener();
        listener.ConnectionRequestEvent += OnConnectionRequest;
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

    public void Start() {
        Start(DefaultPort);
    }

    public void Start(int serverPort) {
        if (State != MultiplayerServerState.Stopped)
            throw new InvalidOperationException("Server is already started");

        port = serverPort;
        SetState(MultiplayerServerState.Preparing);

        try {
            log.Info($"Starting LAN server on port {port}");

            if (!netManager.Start(port)) {
                throw new Exception($"Failed to start server on port {port}");
            }

            // Start discovery service
            var localIp = GetLocalIPAddress();
            var endpoint = new IPEndPoint(localIp, port);
            var serverInfo = new LanServerInfo(
                playerProfileProvider.GetPlayerProfile().PlayerName,
                endpoint,
                serverId
            );

            discovery.StartDiscoveryServer(serverInfo);

            // Create GameObject for updates
            gameObject = UnityObject.CreateStaticWithComponent<LanServerComponent>();

            SetState(MultiplayerServerState.Started);
            log.Info($"LAN server started on {localIp}:{port}");
        } catch (Exception ex) {
            log.Error($"Failed to start LAN server: {ex.Message}");
            Reset();
            SetState(MultiplayerServerState.Error);
            throw;
        }
    }

    public void Stop() {
        if (State == MultiplayerServerState.Stopped)
            return;

        log.Info("Stopping LAN server");
        Reset();
        SetState(MultiplayerServerState.Stopped);
    }

    public void Tick() {
        netManager.PollEvents();
    }

    public void OnDestroy() {
        Reset();
    }

    public void Send(IMultiplayerClientId clientId, IMultiplayerCommand command) {
        if (State != MultiplayerServerState.Started) {
            log.Warning("Cannot send command: server not started");
            return;
        }

        if (!clients.TryGetValue(clientId, out var peer))
            throw new ArgumentException($"Client {clientId} not connected", nameof(clientId));

        try {
            var data = serializer.Serialize(command, MultiplayerCommandOptions.None);
            peer.Send(data, DeliveryMethod.ReliableOrdered);
        } catch (Exception ex) {
            log.Error($"Error sending command to {clientId}: {ex.Message}");
        }
    }

    public void Send(IMultiplayerCommand command, MultiplayerCommandOptions options = MultiplayerCommandOptions.None) {
        if (State != MultiplayerServerState.Started) {
            log.Warning("Cannot broadcast command: server not started");
            return;
        }

        try {
            var data = serializer.Serialize(command, options);

            foreach (var client in clients) {
                // Skip the host if the option is set
                if (options.HasFlag(MultiplayerCommandOptions.SkipHost) && client.Key == GetHostClientId())
                    continue;

                client.Value.Send(data, DeliveryMethod.ReliableOrdered);
            }
        } catch (Exception ex) {
            log.Error($"Error broadcasting command: {ex.Message}");
        }
    }

    private void SetState(MultiplayerServerState newState) {
        if (State == newState) return;

        State = newState;
        StateChanged?.Invoke(newState);
    }

    private void Reset() {
        if (gameObject != null) {
            UnityObject.Destroy(gameObject);
            gameObject = null;
        }

        discovery.StopDiscovery();

        if (netManager.IsRunning) {
            netManager.Stop();
        }

        clients.Clear();
    }

    private void OnConnectionRequest(ConnectionRequest request) {
        try {
            NetDataReader dataReader = request.Data;

            // Make sure we have a valid client ID
            IMultiplayerClientId clientId;

            if (dataReader.AvailableBytes > 0) {
                // Try to read a client ID from the connection data
                string clientIdStr = dataReader.GetString();

                // Validate GUID format
                if (Guid.TryParse(clientIdStr, out var guid)) {
                    clientId = new LanMultiplayerClientId(guid);
                } else {
                    // Invalid format, generate a new one
                    clientId = new LanMultiplayerClientId(Guid.NewGuid());
                    log.Warning($"Received invalid client ID format: {clientIdStr}, generated new ID: {clientId}");
                }
            } else {
                // No client ID provided, generate a new one
                clientId = new LanMultiplayerClientId(Guid.NewGuid());
                log.Info($"No client ID provided in connection request, generated: {clientId}");
            }

            // Accept the connection
            var peer = request.Accept();
            if (peer != null) {
                // Store the client ID in the peer's Tag
                peer.Tag = clientId;
                log.Info($"Accepted connection request from {clientId}");
            }
        } catch (Exception ex) {
            log.Error($"Error processing connection request: {ex.Message}");
            request.Reject();
        }
    }

    private void OnPeerConnected(NetPeer peer) {
        try {
            // The error is happening here when trying to parse a GUID
            // Get the client ID from peer Tag (set during connection)
            if (peer.Tag is IMultiplayerClientId existingClientId) {
                // Tag is already set to the clientId object
                log.Info($"Client connected: {existingClientId} from peer {peer.Id}");
                clients[existingClientId] = peer;
                ClientConnected?.Invoke(existingClientId);
            }
            else if (peer.Tag is string clientIdStr) {
                // Try to parse the string as a GUID only if it's properly formatted
                if (Guid.TryParse(clientIdStr, out var guid)) {
                    var newClientId = new LanMultiplayerClientId(guid);
                    log.Info($"Client connected: {newClientId} from peer {peer.Id}");
                    clients[newClientId] = peer;
                    peer.Tag = newClientId; // Update the Tag to the actual object
                    ClientConnected?.Invoke(newClientId);
                } else {
                    log.Error($"Invalid client ID format: {clientIdStr}");
                    peer.Disconnect();
                }
            }
            else {
                // Generate a new client ID if none is available
                var generatedClientId = new LanMultiplayerClientId(Guid.NewGuid());
                log.Info($"Client connected with generated ID: {generatedClientId} from peer {peer.Id}");
                clients[generatedClientId] = peer;
                peer.Tag = generatedClientId;
                ClientConnected?.Invoke(generatedClientId);
            }
        } catch (Exception ex) {
            log.Error($"Error handling connected peer: {ex.Message}");
            peer.Disconnect();
        }
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        try {
            IMultiplayerClientId? clientId = null;

            // Find the client ID for this peer
            foreach (var kvp in clients) {
                if (kvp.Value == peer) {
                    clientId = kvp.Key;
                    break;
                }
            }

            if (clientId != null) {
                log.Info($"Client disconnected: {clientId}, reason: {disconnectInfo.Reason}");
                clients.Remove(clientId);
                ClientDisconnected?.Invoke(clientId);
            }
        } catch (Exception ex) {
            log.Error($"Error handling disconnected peer: {ex.Message}");
        }
    }

    private void OnNetworkReceiveEvent(
        NetPeer peer,
        NetPacketReader reader,
        byte channel,
        DeliveryMethod deliveryMethod
    ) {
        try {
            if (!(peer.Tag is IMultiplayerClientId clientId)) {
                log.Warning($"Received data from unidentified peer {peer.Id}");
                return;
            }

            var command = serializer.Deserialize(reader);
            if (command == null) return;

            var configuration = commandRegistry.GetCommandConfiguration(command.GetType());

            if (configuration.ExecuteOnServer) {
                // If this command should execute on the server, invoke the event
                CommandReceived?.Invoke(clientId, command);
            } else {
                // Otherwise, forward to other clients
                var options = serializer.GetLastCommandOptions();
                var peers = clients.Values;

                foreach (var targetPeer in peers) {
                    // Skip the sender and optionally the host
                    if (targetPeer == peer) continue;
                    if (options.HasFlag(MultiplayerCommandOptions.SkipHost) &&
                        targetPeer.Tag is IMultiplayerClientId targetId &&
                        targetId == GetHostClientId()) continue;

                    var data = serializer.Serialize(command, options);
                    targetPeer.Send(data, DeliveryMethod.ReliableOrdered);
                }
            }
        } catch (Exception ex) {
            log.Error($"Error processing network message: {ex.Message}");
        }
    }

    private void OnNetworkErrorEvent(IPEndPoint endPoint, SocketError socketError) {
        log.Error($"Network error from {endPoint}: {socketError}");
    }

    private IMultiplayerClientId? GetHostClientId() {
        // Assuming the first client is the host for now
        // This should be improved with proper host identification
        return clients.Count > 0 ? clients.Keys.First() : null;
    }

    private IPAddress GetLocalIPAddress() {
        var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in hostEntry.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                return ip;
            }
        }

        // Fallback to loopback
        return IPAddress.Loopback;
    }
}
