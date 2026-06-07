using System.Collections.Concurrent;
using Minark.Client.Helpers;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Friends;

namespace Minark.Client.Services;

public class FriendClientService : IFriendClientService
{
    private readonly ConcurrentQueue<TaskCompletionSource<FriendListResponse>> _listQueue = new();
    private readonly ConcurrentQueue<TaskCompletionSource<AckResponse>> _requestQueue = new();
    private readonly TcpClientService _tcp;

    public FriendClientService(TcpClientService tcp, PacketDispatcher dispatcher, UserStatusService userStatusService)
    {
        _tcp = tcp;

        dispatcher.Register(PacketType.FriendListChanged, p => Notify(p, ref OnFriendListChanged));
        dispatcher.Register(PacketType.FriendInviteReceived, p => Notify(p, ref OnInviteReceived));
        dispatcher.Register(PacketType.FriendStatusUpdate, p => Notify(p, ref OnFriendStatusChanged));

        // Mis à jour du propre statut du client (déclenché par le GameServer via HTTP)
        dispatcher.Register(PacketType.SelfStatusUpdate, payload =>
        {
            var update = PacketSerializer.DeserializePayload<SelfStatusUpdate>(payload);
            if (update is not null)
            {
                UiThread.Invoke(() => userStatusService.Status = update.Status);
            }
        });

        dispatcher.Register(PacketType.FriendListResponse, payload =>
        {
            var r = PacketSerializer.DeserializePayload<FriendListResponse>(payload)
                    ?? new FriendListResponse { Success = false };
            if (_listQueue.TryDequeue(out var tcs))
            {
                tcs.TrySetResult(r);
            }
        });

        dispatcher.Register(PacketType.FriendRequestResponse, payload =>
        {
            var r = PacketSerializer.DeserializePayload<AckResponse>(payload)
                    ?? new AckResponse { Success = false };
            if (_requestQueue.TryDequeue(out var tcs))
            {
                tcs.TrySetResult(r);
            }
        });
    }

    public event Action<FriendListChanged>? OnFriendListChanged;
    public event Action<FriendInviteReceived>? OnInviteReceived;
    public event Action<FriendStatusUpdate>? OnFriendStatusChanged;

    public async Task<FriendListResponse> GetFriendsAsync(string token)
    {
        var tcs = new TaskCompletionSource<FriendListResponse>();
        _listQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.FriendListRequest, new TokenRequest { Token = token });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<AckResponse> SendFriendRequestAsync(string token, string targetUsername)
    {
        var tcs = new TaskCompletionSource<AckResponse>();
        _requestQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.FriendRequestSend,
            new FriendRequestSend { Token = token, TargetUsername = targetUsername });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public Task AcceptInviteAsync(string token, int friendshipId)
    {
        return _tcp.SendAsync(PacketType.FriendInviteAccept,
            new FriendInviteReply { Token = token, FriendshipId = friendshipId });
    }

    public Task DeclineInviteAsync(string token, int friendshipId)
    {
        return _tcp.SendAsync(PacketType.FriendInviteDecline,
            new FriendInviteReply { Token = token, FriendshipId = friendshipId });
    }

    public Task RemoveFriendAsync(string token, string friendUsername)
    {
        return _tcp.SendAsync(PacketType.FriendRemove,
            new FriendRemove { Token = token, FriendUsername = friendUsername });
    }

    public Task UpdateStatusAsync(string token, UserStatus status)
    {
        return _tcp.SendAsync(PacketType.StatusUpdateRequest,
            new StatusUpdateRequest { Token = token, Status = status });
    }

    private static void Notify<T>(string payload, ref Action<T>? ev) where T : class
    {
        var p = PacketSerializer.DeserializePayload<T>(payload);
        if (p is not null)
        {
            ev?.Invoke(p);
        }
    }
}