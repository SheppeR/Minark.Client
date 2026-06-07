namespace Minark.Game.Shared.Packets;

/// <summary>
///     Enveloppe réseau universelle.
///     Sérialisée en JSON + envoyée via LiteNetLib.
///     Compatible netstandard2.1 (Unity + GameServer).
/// </summary>
public class GamePacket
{
    public GamePacketType Type { get; set; }
    public string Payload { get; set; } = string.Empty;
}