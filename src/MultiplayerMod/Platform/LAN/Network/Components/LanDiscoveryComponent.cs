using System;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Players.Events;
using MultiplayerMod.Platform.LAN.Network.Discovery;

namespace MultiplayerMod.Platform.LAN.Network.Components;

public class LanDiscoveryComponent : MultiplayerMonoBehaviour {
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanDiscoveryComponent>();

    [InjectDependency]
    private EventDispatcher eventDispatcher = null!;

    [InjectDependency]
    private LanServerDiscovery discovery = null!;

    protected override void Awake() {
        base.Awake();
        if (discovery == null) {
            log.Error("LanServerDiscovery is null, LAN discovery will not work");
        } else {
            log.Info("LAN discovery component initialized");
            discovery.ServerDiscovered += OnServerDiscovered;
        }
    }

    private void OnServerDiscovered(LanServerInfo serverInfo) {
        log.Info($"LAN server discovered: {serverInfo.Name} at {serverInfo.Endpoint}");
        var endpoint = new LanServerEndpoint(serverInfo.Endpoint);
        eventDispatcher.Dispatch(new MultiplayerJoinRequestedEvent(endpoint, serverInfo.Name));
    }

    private void OnDestroy() {
        discovery.ServerDiscovered -= OnServerDiscovered;
    }

    private void Update() {
        discovery.Tick();
    }
}
