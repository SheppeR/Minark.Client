using System.Text;
using System.Text.Json;

namespace Minark.Shared.Packets;

public static class PacketSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] Serialize<T>(PacketType type, T payload)
    {
        var packet = new Packet
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload, _options)
        };

        var json = JsonSerializer.Serialize(packet, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public static Packet? Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<Packet>(json, _options);
    }

    public static T? DeserializePayload<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, _options);
    }
}