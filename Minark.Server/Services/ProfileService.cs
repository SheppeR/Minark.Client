using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Data.Entities;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    // CORRECTION : reçoit maintenant le mot de passe en clair (pas un hash SHA256)
    // Le hash SHA256 comme "mot de passe" était le bug de sécurité corrigé en point 2
    public async Task<AckResponse> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return new AckResponse { Success = false, ErrorMessage = "Utilisateur introuvable." };
        }

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
        {
            return new AckResponse { Success = false, ErrorMessage = "Mot de passe actuel incorrect." };
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return new AckResponse
                { Success = false, ErrorMessage = "Le nouveau mot de passe doit faire au moins 8 caractères." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
        return new AckResponse { Success = true };
    }

    // CORRECTION BUG AVATAR : le paquet UpdateAvatarRequest existait dans Shared
    // mais n'était jamais traité côté serveur. Cette méthode est maintenant appelée

    public async Task<UpdateAvatarResponse> UpdateAvatarUrlAsync(int userId, string avatarUrl)
    {
        // OPTIMISATION : ExecuteUpdateAsync — pas de SELECT + UPDATE
        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarUrl, avatarUrl));

        return updated > 0
            ? new UpdateAvatarResponse { Success = true, AvatarUrl = avatarUrl }
            : new UpdateAvatarResponse { Success = false, ErrorMessage = "Utilisateur introuvable." };
    }

    public async Task<AckResponse> BlockUserAsync(int blockerId, string targetUsername)
    {
        var targetId = await db.Users
            .Where(u => u.Username == targetUsername)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (targetId is null)
        {
            return new AckResponse { Success = false, ErrorMessage = "Utilisateur introuvable." };
        }

        if (targetId == blockerId)
        {
            return new AckResponse { Success = false, ErrorMessage = "Vous ne pouvez pas vous bloquer." };
        }

        var exists = await db.BlockedUsers.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == targetId);
        if (exists)
        {
            return new AckResponse { Success = true };
        }

        db.BlockedUsers.Add(new BlockedUser
        {
            BlockerId = blockerId,
            BlockedId = targetId.Value,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new AckResponse { Success = true };
    }

    public async Task<AckResponse> UnblockUserAsync(int blockerId, string targetUsername)
    {
        var targetId = await db.Users
            .Where(u => u.Username == targetUsername)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (targetId is null)
        {
            return new AckResponse { Success = false, ErrorMessage = "Utilisateur introuvable." };
        }

        // OPTIMISATION : ExecuteDeleteAsync
        await db.BlockedUsers
            .Where(b => b.BlockerId == blockerId && b.BlockedId == targetId.Value)
            .ExecuteDeleteAsync();

        return new AckResponse { Success = true };
    }

    public async Task<BlockListResponse> GetBlockListAsync(int userId)
    {
        var names = await db.BlockedUsers
            .Where(b => b.BlockerId == userId)
            .Select(b => b.Blocked.Username)
            .ToListAsync();
        return new BlockListResponse { Success = true, BlockedUsernames = names };
    }

    public async Task<bool> IsBlockedAsync(int userA, int userB)
    {
        return await db.BlockedUsers.AnyAsync(b =>
            (b.BlockerId == userA && b.BlockedId == userB) ||
            (b.BlockerId == userB && b.BlockedId == userA));
    }
}