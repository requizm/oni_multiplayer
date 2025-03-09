using System;
using System.Net;
using LiteNetLib.Utils;

namespace MultiplayerMod.Platform.LAN.Network.Discovery;

[Serializable]
public class LanServerInfo : INetSerializable {
    public string Name { get; private set; } = string.Empty;
    public IPEndPoint Endpoint { get; private set; } = new IPEndPoint(IPAddress.Any, 0);
    public Guid ServerId { get; private set; } = Guid.Empty;

    public LanServerInfo() { }

    public LanServerInfo(string name, IPEndPoint endpoint, Guid serverId) {
        Name = name;
        Endpoint = endpoint;
        ServerId = serverId;
    }

    public void Serialize(NetDataWriter writer) {
        writer.Put(Name);
        writer.Put(Endpoint.Address.ToString());
        writer.Put(Endpoint.Port);
        writer.Put(ServerId.ToByteArray(), 0, 16);
    }

    public void Deserialize(NetDataReader reader) {
        Name = reader.GetString();
        var address = IPAddress.Parse(reader.GetString());
        var port = reader.GetInt();
        Endpoint = new IPEndPoint(address, port);

        // Create a byte array for the GUID (16 bytes)
        byte[] guidBytes = new byte[16];
        reader.GetBytes(guidBytes, 0, 16);
        ServerId = new Guid(guidBytes);
    }

}
