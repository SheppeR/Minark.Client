namespace Minark.Server.Services.Interfaces;

public interface IFriendService
{
    Task<FriendListResponse> GetFriendsAsync(int userId);

    Task<(AckResponse response, int friendshipId)> SendFriendRequestAsync(int requesterId,
        string targetUsername);

    Task<(bool ok, string requesterUsername)> AcceptFriendRequestAsync(int addresseeId, int friendshipId);
    Task<bool> DeclineFriendRequestAsync(int addresseeId, int friendshipId);
    Task<bool> RemoveFriendAsync(int userId, string friendUsername);
    Task<List<int>> GetFriendUserIdsAsync(int userId);
    Task UpdateStatusAsync(int userId, UserStatus status);
}