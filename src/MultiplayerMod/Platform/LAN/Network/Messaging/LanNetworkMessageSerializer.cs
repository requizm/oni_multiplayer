using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using JetBrains.Annotations;
using LiteNetLib.Utils;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Steam.Network.Messaging;
using MultiplayerMod.Platform.Steam.Network.Messaging.Surrogates;

namespace MultiplayerMod.Platform.LAN.Network.Messaging;

[Dependency, UsedImplicitly]
public class LanNetworkMessageSerializer {
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<LanNetworkMessageSerializer>();
    private MultiplayerCommandOptions lastCommandOptions;

    // Get the options from last deserialized command
    public MultiplayerCommandOptions GetLastCommandOptions() => lastCommandOptions;

    // Serializes a command to a NetDataWriter
    public NetDataWriter Serialize(IMultiplayerCommand command, MultiplayerCommandOptions options) {
        var writer = new NetDataWriter();

        try {
            // Create a NetworkMessage with the command and options
            var message = new NetworkMessage(command, options);

            // Use memory stream to efficiently serialize the object
            using var stream = new MemoryStream();
            var formatter = new BinaryFormatter { SurrogateSelector = SerializationSurrogates.Selector };
            formatter.Serialize(stream, message);

            // Write the serialized data to the writer
            writer.Put(stream.ToArray());

            return writer;
        }
        catch (Exception ex) {
            log.Error($"Error serializing command {command.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    // Deserializes a command from a NetDataReader
    public IMultiplayerCommand? Deserialize(NetDataReader reader) {
        try {
            // Get the serialized data
            var data = reader.GetRemainingBytes();

            // Deserialize using Binary Formatter
            using var stream = new MemoryStream(data);
            var formatter = new BinaryFormatter { SurrogateSelector = SerializationSurrogates.Selector };
            var message = (NetworkMessage)formatter.Deserialize(stream);

            // Store options for later use
            lastCommandOptions = message.Options;

            return message.Command;
        }
        catch (Exception ex) {
            log.Error($"Error deserializing command: {ex.Message}");
            return null;
        }
    }
}
