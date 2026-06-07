using LiteNetLib;
using Microsoft.EntityFrameworkCore;
using Minark.GameServer.Data;
using Minark.GameServer.Network;
using Minark.GameServer.Services;

namespace Minark.GameServer.Handlers;

public class FriendListHandler(
    IDbContextFactory<GameDbContext> dbFactory,
    PlayerRegistry registry,
    IServerSender sender,
    ILogger<FriendListHandler> log) : IPacketHandler
{
    public GamePacketType PacketType => GamePacketType.FriendListRequest;

    public async Task HandleAsync(NetPeer peer, GamePacket packet, CancellationToken ct)
    {
        if (!registry.TryGetByPeer(peer.Id, out var player) || player is null)
        {
            sender.Send(peer, GamePacketType.FriendListResponse,
                new FriendListResponse { Success = false, Error = "Non authentifié." });
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Charger les données des amis depuis DB
        var friendUsers = await db.Users
            .AsNoTracking()
            .Where(u => player.FriendIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.AvatarUrl })
            .ToListAsync(ct);

        var friends = friendUsers.Select(u => new GameFriendDto
        {
            Id = u.Id,
            Username = u.Username,
            AvatarUrl = u.AvatarUrl,
            // Si l'ami est connecté au GameServer → InGame, sinon Offline
            Status = registry.IsUserConnected(u.Id)
                ? GameUserStatus.InGame
                : GameUserStatus.Offline
        }).ToArray();

        log.LogDebug("FriendList pour {Username} : {Count} ami(s)", player.Username, friends.Length);

        sender.Send(peer, GamePacketType.FriendListResponse, new FriendListResponse
        {
            Success = true,
            Friends = friends
        });
    }
}