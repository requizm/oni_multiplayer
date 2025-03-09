using System.Collections.Generic;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Platform.LAN.Network.Discovery;
using UnityEngine;

namespace MultiplayerMod.Platform.LAN;

[Dependency, UsedImplicitly]
[DependencyPriority(100)]
public class LanMultiplayerOperations : IMultiplayerOperations {
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanMultiplayerOperations>();
    private readonly LanServerDiscovery discovery;

    public LanMultiplayerOperations(LanServerDiscovery discovery) {
        this.discovery = discovery;
    }

    public void Join() {
        discovery.StartDiscoveryClient();

        // When a server is found, the LanDiscoveryComponent will handle it
        // and dispatch the appropriate events for the UI to show the server
    }
}
