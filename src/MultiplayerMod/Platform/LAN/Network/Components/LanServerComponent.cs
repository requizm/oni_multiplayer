using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Unity;

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace MultiplayerMod.Platform.LAN.Network.Components;

public class LanServerComponent : MultiplayerMonoBehaviour {
    [InjectDependency]
    private LanServer server = null!;

    private void Update() => server.Tick();
    private void OnDestroy() => server.OnDestroy();
}
