using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Minark.Game.Shared.Packets;

public static class GamePacketSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    public static byte[] Serialize<T>(GamePacketType type, T payload)
    {
        var packet = new GamePacket
        {
            Type = type,
            Payload = JsonConvert.SerializeObject(payload, Settings)
        };
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet, Settings));
    }

    public static GamePacket? Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<GamePacket>(json, Settings);
    }

    public static GamePacket? Deserialize(byte[] data, int offset, int length)
    {
        var json = Encoding.UTF8.GetString(data, offset, length);
        return JsonConvert.DeserializeObject<GamePacket>(json, Settings);
    }

    public static T? DeserializePayload<T>(string payload)
    {
        return JsonConvert.DeserializeObject<T>(payload, Settings);
    }
}