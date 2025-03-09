using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.ModRuntime.Loader;
using MultiplayerMod.Platform.LAN.Network.Components;

namespace MultiplayerMod.Platform.LAN;

[UsedImplicitly]
[ModComponentOrder(ModComponentOrder.Platform)]
[DependencyPriority(100)]
public class LanPlatformConfigurer : IModComponentConfigurer {
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanPlatformConfigurer>();

    public void Configure(DependencyContainerBuilder builder) {
        log.Info("Configuring LAN support");

        // Register the LAN discovery component at container creation time
        builder.ContainerCreated += _ => {
            UnityObject.CreateStaticWithComponent<LanDiscoveryComponent>();
        };
    }
}
