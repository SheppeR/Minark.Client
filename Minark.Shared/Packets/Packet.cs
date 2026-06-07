namespace Minark.Shared.Packets;

public class Packet
{
    public PacketType Type { get; init; }
    public string Payload { get; init; } = string.Empty;
}