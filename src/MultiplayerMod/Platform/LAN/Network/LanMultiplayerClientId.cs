using System;
using MultiplayerMod.Network;

namespace MultiplayerMod.Platform.LAN.Network;

[Serializable]
public record LanMultiplayerClientId(Guid Id) : IMultiplayerClientId {
    public bool Equals(IMultiplayerClientId other) {
        return other is LanMultiplayerClientId lanId && lanId.Equals(this);
    }

    public override string ToString() => Id.ToString();
}
