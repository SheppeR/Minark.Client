using Minark.Shared.Packets;
using Minark.Shared.Packets.Auth;
using Minark.Shared.Packets.Friends;

namespace Minark.Client.Services.Interfaces;

public interface IProfileClientService
{
    Task<AckResponse> ChangePasswordAsync(string token, string oldPassword, string newPassword);
    Task<UpdateAvatarResponse> UploadAvatarAsync(string token, string filePath);
    Task<UpdateAvatarResponse> UpdateAvatarAsync(string token, string filePath);
    Task<AckResponse> BlockUserAsync(string token, string targetUsername);
    Task<AckResponse> UnblockUserAsync(string token, string targetUsername);
    Task<BlockListResponse> GetBlockListAsync(string token);
}