using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Unity;

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace MultiplayerMod.Platform.LAN.Network.Components;

public class LanClientComponent : MultiplayerMonoBehaviour {
    [InjectDependency]
    private LanClient client = null!;

    private void Update() => client.Tick();
    private void OnDestroy() => client.OnDestroy();
}
