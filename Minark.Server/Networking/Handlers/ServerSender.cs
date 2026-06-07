using Minark.Server.Services.Interfaces;
using Serilog;
using WatsonTcp;

namespace Minark.Server.Networking.Handlers;

/// <summary>Envoie des paquets aux clients via WatsonTcp.</summary>
public sealed class ServerSender(
    WatsonTcpServer server,
    ISessionStore sessionStore,
    IServiceScopeFactory scopeFactory) : IServerSender
{
    public async Task SendAsync<T>(Guid clientGuid, PacketType type, T payload)
    {
        var data = PacketSerializer.Serialize(type, payload);
        await server.SendAsync(clientGuid, data);
    }

    public async Task PushStatusToFriendsAsync(int userId, string username, UserStatus status)
    {
        Log.Information("ServerSender: Pushing status for {Username} ({UserId}) = {Status}", username, userId, status);

        await using var scope = scopeFactory.CreateAsyncScope();
        var friends = scope.ServiceProvider.GetRequiredService<IFriendService>();

        var friendIds = await friends.GetFriendUserIdsAsync(userId);
        Log.Information("ServerSender: Found {Count} friends for {Username}", friendIds.Count, username);

        var packet = PacketSerializer.Serialize(PacketType.FriendStatusUpdate,
            new FriendStatusUpdate { Username = username, Status = status });

        foreach (var friendId in friendIds)
        {
            var friendGuid = sessionStore.FindClientByUserId(friendId);
            if (friendGuid is null)
            {
                Log.Debug("ServerSender: Friend {FriendId} is offline, skipping", friendId);
                continue;
            }

            // Vérifier que WatsonTcp connaît encore ce GUID avant d'envoyer :
            // évite KeyNotFoundException sur les sockets zombies.
            if (!server.IsClientConnected(friendGuid.Value))
            {
                Log.Warning(
                    "ServerSender: Friend {FriendId} has a stale GUID {Guid} in session store — cleaning up",
                    friendId, friendGuid.Value);
                sessionStore.RemoveClient(friendGuid.Value);
                continue;
            }

            try
            {
                await server.SendAsync(friendGuid.Value, packet);
                Log.Debug("ServerSender: Status sent to friend {FriendId}", friendId);
            }
            catch (Exception ex)
            {
                // Dernière ligne de défense : le client a pu se déconnecter pile entre
                // le check et le send. On nettoie le mapping et on continue.
                Log.Warning(ex, "ServerSender: Send to friend {FriendId} failed, cleaning up stale mapping", friendId);
                sessionStore.RemoveClient(friendGuid.Value);
            }
        }
    }

    public async Task PushFriendListChangedAsync(int actorId, string actorUsername, string otherUsername, string reason)
    {
        var actorGuid = sessionStore.FindClientByUserId(actorId);
        if (actorGuid is not null)
        {
            await SendAsync(actorGuid.Value, PacketType.FriendListChanged,
                new FriendListChanged { Reason = reason, OtherUsername = otherUsername });
        }

        var otherEntry = sessionStore.GetClientByUsername(otherUsername);
        if (otherEntry.HasValue)
        {
            await SendAsync(otherEntry.Value.ClientGuid, PacketType.FriendListChanged,
                new FriendListChanged { Reason = reason, OtherUsername = actorUsername });
        }
    }

    public async Task DisconnectClientAsync(Guid clientGuid)
    {
        try
        {
            if (server.IsClientConnected(clientGuid))
            {
                // WatsonTcp n'expose que la version async ; sendNotice:false car on vient
                // déjà d'envoyer notre propre SessionInvalidated juste avant.
                await server.DisconnectClientAsync(clientGuid, MessageStatus.Removed, false);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "ServerSender: DisconnectClientAsync — client {Guid} already gone", clientGuid);
        }

        // De toute façon, nettoyer le mapping au cas où ClientDisconnected ne se déclenche pas.
        sessionStore.RemoveClient(clientGuid);
    }
}