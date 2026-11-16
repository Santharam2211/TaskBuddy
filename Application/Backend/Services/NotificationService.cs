using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TimeBuddy.Backend.Data;
using TimeBuddy.Backend.DTOs;
using TimeBuddy.Backend.Models;

namespace TimeBuddy.Backend.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(int userId, int? scheduleId, string type, string message);
        Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, int limit = 50);
        Task<bool> MarkAsReadAsync(int userId, int notificationId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Notification> CreateNotificationAsync(int userId, int? scheduleId, string type, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                ScheduleId = scheduleId,
                NotificationType = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification;
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, int limit = 50)
        {
            var notifications = await _context.Notifications
                .Include(n => n.Schedule)
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    UserId = n.UserId,
                    ScheduleId = n.ScheduleId,
                    TaskTitle = n.Schedule != null ? n.Schedule.TaskTitle : null,
                    NotificationType = n.NotificationType,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return notifications;
        }

        public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notification == null) return false;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!unread.Any()) return true;

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
}
