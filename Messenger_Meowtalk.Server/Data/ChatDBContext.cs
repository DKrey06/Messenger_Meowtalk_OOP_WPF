using Microsoft.EntityFrameworkCore;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Server.Data
{
    public class ChatDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserChat> UserChats { get; set; }
        public DbSet<EncryptedMessage> EncryptedMessages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var projectRoot = Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.Parent?.FullName
                  ?? Directory.GetCurrentDirectory();
            var dbPath = Path.Combine(projectRoot, "meowtalk.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserChat>()
                .HasKey(uc => uc.Id);

            modelBuilder.Entity<UserChat>()
                .HasIndex(uc => new { uc.UserId, uc.ChatId })
                .IsUnique();

            modelBuilder.Entity<UserChat>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.UserChats)
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserChat>()
                .HasOne(uc => uc.Chat)
                .WithMany(c => c.UserChats)
                .HasForeignKey(uc => uc.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>(entity =>
            {
                entity.Property(m => m.MediaType)
                    .HasDefaultValue(string.Empty);
            });

            modelBuilder.Entity<Chat>()
                .Ignore(c => c.LastMessage)
                .Ignore(c => c.LastMessageTime)
                .Ignore(c => c.LastMessageTimestamp);
        }
    }
}