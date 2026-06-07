using Microsoft.EntityFrameworkCore;
using Minark.Server.Data.Entities;

namespace Minark.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<News> News => Set<News>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UnreadMessage> UnreadMessages => Set<UnreadMessage>();
    public DbSet<NewsReaction> NewsReactions => Set<NewsReaction>();
    public DbSet<NewsComment> NewsComments => Set<NewsComment>();
    public DbSet<NewsMedia> NewsMedias => Set<NewsMedia>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Friendship>(e =>
        {
            e.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();

            e.HasOne(f => f.Requester)
                .WithMany(u => u.FriendshipsInitiated)
                .HasForeignKey(f => f.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.Addressee)
                .WithMany(u => u.FriendshipsReceived)
                .HasForeignKey(f => f.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Index optimisé
            e.HasIndex(f => f.Status);
        });

        modelBuilder.Entity<Session>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(m => new { m.SenderId, m.ReceiverId });

            // ✅ Index optimisés pour performance
            e.HasIndex(m => m.SentAt).IsDescending();
            e.HasIndex(m => m.IsDeleted);
            e.HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt }).IsDescending(false, false, true);
        });

        modelBuilder.Entity<News>(e =>
        {
            // Forcer Content en TEXT pour supporter les articles longs avec plusieurs images [img]...[/img]
            e.Property(n => n.Content).HasColumnType("TEXT");
            // ImageUrl aussi en TEXT pour les URLs longues
            e.Property(n => n.ImageUrl).HasColumnType("VARCHAR(2048)");

            // ✅ Index optimisés pour performance
            e.HasIndex(n => n.PublishedAt).IsDescending();
            e.HasIndex(n => n.Author);
        });

        modelBuilder.Entity<UnreadMessage>(e =>
        {
            e.HasOne(u => u.Recipient)
                .WithMany()
                .HasForeignKey(u => u.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(u => u.Message)
                .WithMany()
                .HasForeignKey(u => u.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Index optimisés pour performance
            e.HasIndex(u => new { u.RecipientId, u.FromUsername });

            e.HasIndex(u => new { u.RecipientId, u.MessageId }).IsUnique();
        });


        modelBuilder.Entity<NewsReaction>(e =>
        {
            e.HasIndex(r => new { r.NewsId, r.UserId }).IsUnique();
            e.HasOne(r => r.News).WithMany().HasForeignKey(r => r.NewsId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

            // ✅ Index optimisé
            e.HasIndex(r => r.NewsId);
        });

        modelBuilder.Entity<NewsComment>(e =>
        {
            e.HasOne(c => c.News).WithMany().HasForeignKey(c => c.NewsId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.Content).HasColumnType("TEXT");

            // ✅ Index optimisé
            e.HasIndex(c => c.NewsId);
        });

        modelBuilder.Entity<NewsMedia>(e =>
        {
            e.HasOne(m => m.News).WithMany().HasForeignKey(m => m.NewsId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.NewsId, m.SortOrder });
            e.Property(m => m.Url).HasColumnType("TEXT");
        });

        modelBuilder.Entity<MessageReaction>(e =>
        {
            e.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji }).IsUnique();
            e.HasOne(r => r.Message).WithMany(m => m.Reactions)
                .HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User).WithMany()
                .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            // utf8mb4_bin obligatoire : unicode_ci considère tous les emojis 4-bytes comme équivalents
            e.Property(r => r.Emoji).UseCollation("utf8mb4_bin");

            // ✅ Index optimisé
            e.HasIndex(r => r.MessageId);
        });
    }
}