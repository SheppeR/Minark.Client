namespace Minark.Server.Services.Interfaces;

public interface IProfileService
{
    Task<AckResponse> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    Task<AckResponse> BlockUserAsync(int blockerId, string targetUsername);
    Task<AckResponse> UnblockUserAsync(int blockerId, string targetUsername);
    Task<BlockListResponse> GetBlockListAsync(int userId);
    Task<bool> IsBlockedAsync(int userA, int userB);
    Task<UpdateAvatarResponse> UpdateAvatarUrlAsync(int userId, string avatarUrl);
}