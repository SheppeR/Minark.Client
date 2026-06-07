using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

/// <summary>
///     Relaie un événement <see cref="PacketType.NewsChanged" /> déclenché par un client
///     en broadcast global. Historiquement utilisé pour forcer un rafraîchissement des listes.
///     Candidat à la suppression : ce paquet est normalement une notification serveur→client,
///     pas un message client→serveur. Voir la note dans le README.
/// </summary>
public sealed class NewsChangedHandler(INewsService news) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsChanged;

    public Task HandleAsync(Guid clientGuid, string payload)
    {
        return news.BroadcastNewsChangedAsync(0, "updated");
    }
}