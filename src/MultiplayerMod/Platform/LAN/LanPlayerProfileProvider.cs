using System;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Multiplayer.Players;

namespace MultiplayerMod.Platform.LAN;

[Dependency, UsedImplicitly]
[DependencyPriority(100)]
public class LanPlayerProfileProvider : IPlayerProfileProvider {
    private readonly Lazy<PlayerProfile> profile;

    public LanPlayerProfileProvider() {
        profile = new Lazy<PlayerProfile>(() => {
            var hostName = GetUniqueHostName();
            return new PlayerProfile(hostName);
        });
    }

    public PlayerProfile GetPlayerProfile() => profile.Value;

    private string GetUniqueHostName() {
        try {
            // Try to use the computer name as the base
            string baseName = Environment.MachineName;
            if (string.IsNullOrEmpty(baseName)) {
                baseName = "Player";
            }

            return baseName;
        }
        catch {
            // Fallback if we can't get the machine name
            return $"Player-{new Random().Next(1000, 9999)}";
        }
    }
}
