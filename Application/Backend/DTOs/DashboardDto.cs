using System;
using System.Collections.Generic;

namespace TimeBuddy.Backend.DTOs
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public int? ScheduleId { get; set; }
        public string? TaskTitle { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardSummaryDto
    {
        public int TotalTasks { get; set; }
        public int UpcomingTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int SharedTasks { get; set; }
        public int PendingFriendRequests { get; set; }
    }

    public class DashboardRecentDto
    {
        public List<ScheduleDto> RecentlyCompletedTasks { get; set; } = new();
        public List<NotificationDto> RecentNotifications { get; set; } = new();
    }
}
