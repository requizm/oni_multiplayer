using System;
using System.Net;
using MultiplayerMod.Network;

namespace MultiplayerMod.Platform.LAN.Network;

public record LanServerEndpoint(IPEndPoint EndPoint) : IMultiplayerEndpoint {
    public override string ToString() => $"{EndPoint.Address}:{EndPoint.Port}";
}
