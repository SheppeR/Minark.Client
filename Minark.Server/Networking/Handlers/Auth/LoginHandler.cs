using Minark.Server.Services.Interfaces;
using Serilog;

namespace Minark.Server.Networking.Handlers.Auth;

public sealed class LoginHandler(
    IAuthService auth,
    ISessionStore sessionStore,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.LoginRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<LoginRequest>(payload);
        if (req is null)
        {
            return;
        }

        var resp = await auth.LoginAsync(req, clientGuid.ToString());
        await sender.SendAsync(clientGuid, PacketType.LoginResponse, resp);

        if (resp is not { Success: true, User: not null })
        {
            return;
        }

        // Installer la session. Si le même user était connecté ailleurs, on récupère l'ancien
        // GUID pour le déconnecter proprement (notification + close socket).
        var evictedGuid = sessionStore.SetUserForClient(clientGuid, resp.User.Id, resp.User.Username);

        if (evictedGuid is not null)
        {
            Log.Information(
                "LoginHandler: evicting previous session of {Username} (old={OldGuid}, new={NewGuid})",
                resp.User.Username, evictedGuid.Value, clientGuid);

            try
            {
                await sender.SendAsync(evictedGuid.Value, PacketType.SessionInvalidated,
                    new SessionInvalidatedNotification
                    {
                        Reason = "Votre compte vient d'être utilisé depuis un autre appareil."
                    });
            }
            catch
            {
                // Tant pis, l'ancien client n'est peut-être plus joignable — on enchaîne sur le disconnect.
            }

            await sender.DisconnectClientAsync(evictedGuid.Value);
        }

        await sender.PushStatusToFriendsAsync(resp.User.Id, resp.User.Username, UserStatus.Online);
    }
}