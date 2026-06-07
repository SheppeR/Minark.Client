namespace Minark.Server.Networking.Handlers;

/// <summary>Contrat unique pour tous les handlers de paquets.</summary>
public interface IPacketHandler
{
    PacketType PacketType { get; }
    Task HandleAsync(Guid clientGuid, string payload);
}