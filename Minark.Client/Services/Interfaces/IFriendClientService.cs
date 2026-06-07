using Minark.Shared.Packets;
using Minark.Shared.Packets.Friends;

namespace Minark.Client.Services.Interfaces;

public interface IFriendClientService
{
    event Action<FriendListChanged>? OnFriendListChanged;
    event Action<FriendInviteReceived>? OnInviteReceived;
    event Action<FriendStatusUpdate>? OnFriendStatusChanged;

    Task<FriendListResponse> GetFriendsAsync(string token);
    Task<AckResponse> SendFriendRequestAsync(string token, string targetUsername);
    Task AcceptInviteAsync(string token, int friendshipId);
    Task DeclineInviteAsync(string token, int friendshipId);
    Task RemoveFriendAsync(string token, string friendUsername);
    Task UpdateStatusAsync(string token, UserStatus status);
}