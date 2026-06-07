using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Data.Entities;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class FriendService(AppDbContext db, ILogger<FriendService> logger) : IFriendService
{
    // ── OPTIMISATION : une seule requête SQL avec projection ──────────────────
    // AVANT : Include(Requester) + Include(Addressee) chargeait tous les Users en mémoire,
    //         puis C# filtrait. Sur 500 amis = 1000 entités User chargées pour rien.
    // APRÈS : projection directe en SQL avec UNION. EF Core génère deux SELECT + UNION ALL.
    public async Task<FriendListResponse> GetFriendsAsync(int userId)
    {
        // Amis acceptés (je suis requester) — exclut les utilisateurs bloqués dans les deux sens
        var asRequester = db.Friendships
            .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Accepted
                                                && !db.BlockedUsers.Any(b =>
                                                    (b.BlockerId == userId && b.BlockedId == f.AddresseeId) ||
                                                    (b.BlockedId == userId && b.BlockerId == f.AddresseeId)))
            .Select(f => new FriendDto
            {
                Id = f.Addressee.Id,
                Username = f.Addressee.Username,
                Status = (UserStatus)f.Addressee.Status,
                AvatarUrl = f.Addressee.AvatarUrl,
                FriendsSince = f.CreatedAt
            });

        // Amis acceptés (je suis addressee) — exclut les utilisateurs bloqués dans les deux sens
        var asAddressee = db.Friendships
            .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Accepted
                                                && !db.BlockedUsers.Any(b =>
                                                    (b.BlockerId == userId && b.BlockedId == f.RequesterId) ||
                                                    (b.BlockedId == userId && b.BlockerId == f.RequesterId)))
            .Select(f => new FriendDto
            {
                Id = f.Requester.Id,
                Username = f.Requester.Username,
                Status = (UserStatus)f.Requester.Status,
                AvatarUrl = f.Requester.AvatarUrl,
                FriendsSince = f.CreatedAt
            });

        var friends = await asRequester.Concat(asAddressee)
            .OrderBy(f => f.Username)
            .ToListAsync();

        // Invitations reçues en attente — projection directe, pas d'Include
        var pending = await db.Friendships
            .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
            .Select(f => new PendingInviteDto
            {
                FriendshipId = f.Id,
                FromUsername = f.Requester.Username,
                SentAt = f.CreatedAt
            })
            .ToListAsync();

        return new FriendListResponse { Success = true, Friends = friends, PendingInvites = pending };
    }

    public async Task<(AckResponse response, int friendshipId)> SendFriendRequestAsync(
        int requesterId, string targetUsername)
    {
        var target = await db.Users.FirstOrDefaultAsync(u => u.Username == targetUsername);
        if (target is null)
        {
            return (new AckResponse { Success = false, ErrorMessage = "Utilisateur introuvable." }, 0);
        }

        if (target.Id == requesterId)
        {
            return (
                new AckResponse
                    { Success = false, ErrorMessage = "Vous ne pouvez pas vous ajouter vous-même." }, 0);
        }

        var existing = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == requesterId && f.AddresseeId == target.Id) ||
            (f.RequesterId == target.Id && f.AddresseeId == requesterId));

        if (existing is not null)
        {
            var msg = existing.Status == FriendshipStatus.Pending
                ? "Une demande est déjà en attente."
                : "Vous êtes déjà amis.";
            return (new AckResponse { Success = false, ErrorMessage = msg }, 0);
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = target.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();

        logger.LogInformation("Friend request {RequesterId} → {Target}", requesterId, targetUsername);
        return (new AckResponse { Success = true }, friendship.Id);
    }

    public async Task<(bool ok, string requesterUsername)> AcceptFriendRequestAsync(int addresseeId, int friendshipId)
    {
        var friendship = await db.Friendships
            .Include(f => f.Requester)
            .FirstOrDefaultAsync(f => f.Id == friendshipId && f.AddresseeId == addresseeId
                                                           && f.Status == FriendshipStatus.Pending);
        if (friendship is null)
        {
            return (false, string.Empty);
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("Friend request {Id} accepted by {UserId}", friendshipId, addresseeId);
        return (true, friendship.Requester.Username);
    }

    public async Task<bool> DeclineFriendRequestAsync(int addresseeId, int friendshipId)
    {
        var friendship = await db.Friendships.FirstOrDefaultAsync(f =>
            f.Id == friendshipId && f.AddresseeId == addresseeId && f.Status == FriendshipStatus.Pending);
        if (friendship is null)
        {
            return false;
        }

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync();
        logger.LogInformation("Friend request {Id} declined", friendshipId);
        return true;
    }

    public async Task<bool> RemoveFriendAsync(int userId, string friendUsername)
    {
        // OPTIMISATION : une seule requête avec JOIN implicite plutôt que deux requêtes séparées
        var friendship = await db.Friendships
            .Where(f =>
                (f.RequesterId == userId && f.Addressee.Username == friendUsername) ||
                (f.AddresseeId == userId && f.Requester.Username == friendUsername))
            .FirstOrDefaultAsync();

        if (friendship is null)
        {
            return false;
        }

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync();
        return true;
    }

    // OPTIMISATION : projection directe — pas de chargement des entités User
    public async Task<List<int>> GetFriendUserIdsAsync(int userId)
    {
        return await db.Friendships
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId)
                        && f.Status == FriendshipStatus.Accepted)
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    // OPTIMISATION : ExecuteUpdateAsync — pas de SELECT puis UPDATE, une seule requête SQL
    public async Task UpdateStatusAsync(int userId, UserStatus status)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, (int)status));
    }
}