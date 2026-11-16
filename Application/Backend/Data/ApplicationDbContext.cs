using Microsoft.EntityFrameworkCore;
using TimeBuddy.Backend.Models;

namespace TimeBuddy.Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<ScheduleParticipant> ScheduleParticipants => Set<ScheduleParticipant>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(u => u.CreatedAt).IsRequired();
            });

            // FriendRequests
            modelBuilder.Entity<FriendRequest>(entity =>
            {
                entity.HasKey(fr => fr.RequestId);
                entity.Property(fr => fr.Status).IsRequired().HasMaxLength(20);

                entity.HasOne(fr => fr.Sender)
                      .WithMany(u => u.SentFriendRequests)
                      .HasForeignKey(fr => fr.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(fr => fr.Receiver)
                      .WithMany(u => u.ReceivedFriendRequests)
                      .HasForeignKey(fr => fr.ReceiverId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(fr => new { fr.SenderId, fr.ReceiverId });
            });

            // Schedules
            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.HasKey(s => s.ScheduleId);
                entity.Property(s => s.TaskTitle).IsRequired().HasMaxLength(200);
                entity.Property(s => s.Description).HasMaxLength(2000);
                entity.Property(s => s.Priority).IsRequired().HasMaxLength(20);
                entity.Property(s => s.ScheduleType).IsRequired().HasMaxLength(20);
                entity.Property(s => s.Status).IsRequired().HasMaxLength(20);

                entity.HasOne(s => s.Creator)
                      .WithMany(u => u.CreatedSchedules)
                      .HasForeignKey(s => s.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.CompletedByUser)
                      .WithMany(u => u.CompletedSchedules)
                      .HasForeignKey(s => s.CompletedBy)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);

                entity.HasIndex(s => s.EndTime);
                entity.HasIndex(s => s.Status);
            });

            // ScheduleParticipants
            modelBuilder.Entity<ScheduleParticipant>(entity =>
            {
                entity.HasKey(sp => sp.ParticipantId);
                entity.Property(sp => sp.InvitationStatus).IsRequired().HasMaxLength(20);

                entity.HasOne(sp => sp.Schedule)
                      .WithMany(s => s.Participants)
                      .HasForeignKey(sp => sp.ScheduleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sp => sp.User)
                      .WithMany(u => u.Participations)
                      .HasForeignKey(sp => sp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(sp => new { sp.ScheduleId, sp.UserId }).IsUnique();
            });

            // Notifications
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.NotificationId);
                entity.Property(n => n.NotificationType).IsRequired().HasMaxLength(50);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(500);

                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Schedule)
                      .WithMany(s => s.Notifications)
                      .HasForeignKey(n => n.ScheduleId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasIndex(n => new { n.UserId, n.IsRead });
            });

            // EmailLogs
            modelBuilder.Entity<EmailLog>(entity =>
            {
                entity.HasKey(el => el.EmailLogId);
                entity.Property(el => el.NotificationType).IsRequired().HasMaxLength(50);
                entity.Property(el => el.DeliveryStatus).IsRequired().HasMaxLength(20);
                entity.Property(el => el.ErrorMessage).HasMaxLength(1000);

                entity.HasOne(el => el.User)
                      .WithMany(u => u.EmailLogs)
                      .HasForeignKey(el => el.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(el => el.Schedule)
                      .WithMany(s => s.EmailLogs)
                      .HasForeignKey(el => el.ScheduleId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                // Idempotency: Unique constraint for (ScheduleId, UserId, NotificationType)
                entity.HasIndex(el => new { el.ScheduleId, el.UserId, el.NotificationType }).IsUnique();
            });
        }
    }
}
