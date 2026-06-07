using Minark.Server.Services.Interfaces;
using WatsonTcp;

namespace Minark.Server.Networking;

public interface IClientBroadcaster
{
    Task BroadcastAsync(byte[] data);
}

public class ClientBroadcaster(WatsonTcpServer server, ISessionStore sessions) : IClientBroadcaster
{
    public async Task BroadcastAsync(byte[] data)
    {
        foreach (var clientGuid in sessions.GetOnlineClients())
        {
            try
            {
                await server.SendAsync(clientGuid, data);
            }
            catch
            {
                /* client déconnecté */
            }
        }
    }
}