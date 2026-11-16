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
    public interface IScheduleService
    {
        Task<(bool Success, string Message, ScheduleDto? Data)> CreateScheduleAsync(int creatorId, CreateScheduleDto dto);
        Task<List<ScheduleDto>> GetUserSchedulesAsync(int currentUserId, string? status = null, string? priority = null, DateTime? fromDate = null, DateTime? toDate = null, string? search = null);
        Task<List<ScheduleDto>> GetSharedSchedulesAsync(int currentUserId);
        Task<(bool Success, string Message, ScheduleDto? Data)> GetScheduleByIdAsync(int currentUserId, int scheduleId);
        Task<(bool Success, string Message, ScheduleDto? Data)> UpdateScheduleAsync(int currentUserId, int scheduleId, UpdateScheduleDto dto);
        Task<(bool Success, string Message)> DeleteScheduleAsync(int currentUserId, int scheduleId);
        Task<(bool Success, string Message, ScheduleDto? Data)> MarkAsCompletedAsync(int currentUserId, int scheduleId);
        Task<(bool Success, string Message)> AddParticipantAsync(int currentUserId, int scheduleId, int friendUserId);
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(int currentUserId);
        Task<List<ScheduleDto>> GetUpcomingTasksAsync(int currentUserId, int limit = 5);
        Task<DashboardRecentDto> GetDashboardRecentAsync(int currentUserId);
    }

    public class ScheduleService : IScheduleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFriendService _friendService;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;

        public ScheduleService(
            ApplicationDbContext context,
            IFriendService friendService,
            IEmailService emailService,
            INotificationService notificationService)
        {
            _context = context;
            _friendService = friendService;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        public async Task<(bool Success, string Message, ScheduleDto? Data)> CreateScheduleAsync(int creatorId, CreateScheduleDto dto)
        {
            if (dto.EndTime <= dto.StartTime)
            {
                return (false, "End time must be later than start time.", null);
            }

            var creator = await _context.Users.FindAsync(creatorId);
            if (creator == null)
            {
                return (false, "Creator user not found.", null);
            }

            bool isShared = dto.ScheduleType.Equals("Shared", StringComparison.OrdinalIgnoreCase);

            var schedule = new Schedule
            {
                CreatedBy = creatorId,
                TaskTitle = dto.TaskTitle.Trim(),
                Description = dto.Description?.Trim(),
                StartTime = dto.StartTime.ToUniversalTime(),
                EndTime = dto.EndTime.ToUniversalTime(),
                Priority = dto.Priority,
                ScheduleType = isShared ? "Shared" : "Personal",
                Status = "Scheduled",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // If shared, validate and add participants
            if (isShared && dto.ParticipantUserIds != null && dto.ParticipantUserIds.Any())
            {
                // Only accepted friends can participate!
                var validFriendIds = await _friendService.FilterToFriendsOnlyAsync(creatorId, dto.ParticipantUserIds);

                foreach (var friendId in validFriendIds)
                {
                    schedule.Participants.Add(new ScheduleParticipant
                    {
                        UserId = friendId,
                        InvitationStatus = "Accepted",
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            _context.Schedules.Add(schedule);
            await _context.SaveChangesAsync();

            // Notify participants if shared
            if (isShared && schedule.Participants.Any())
            {
                var participantUserIds = schedule.Participants.Select(p => p.UserId).ToList();
                var recipientUsers = await _context.Users.Where(u => participantUserIds.Contains(u.UserId)).ToListAsync();

                foreach (var recipient in recipientUsers)
                {
                    // In-app notification
                    await _notificationService.CreateNotificationAsync(
                        recipient.UserId,
                        schedule.ScheduleId,
                        "ScheduleInvite",
                        $"{creator.FullName} added you to a shared task: '{schedule.TaskTitle}'.");

                    // Email notification
                    try
                    {
                        await _emailService.SendScheduleInvitationNotificationAsync(schedule, recipient, creator.FullName);
                    }
                    catch { }
                }
            }

            var scheduleDto = await MapToDtoAsync(schedule.ScheduleId, creatorId);
            return (true, "Schedule created successfully.", scheduleDto);
        }

        public async Task<List<ScheduleDto>> GetUserSchedulesAsync(
            int currentUserId,
            string? status = null,
            string? priority = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? search = null)
        {
            var query = _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .Where(s => s.CreatedBy == currentUserId || s.Participants.Any(p => p.UserId == currentUserId));

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(s => s.Status.ToLower() == status.Trim().ToLower());
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                query = query.Where(s => s.Priority.ToLower() == priority.Trim().ToLower());
            }

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.ToUniversalTime();
                query = query.Where(s => s.EndTime >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.ToUniversalTime();
                query = query.Where(s => s.StartTime <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(s => s.TaskTitle.ToLower().Contains(searchLower) ||
                                         (s.Description != null && s.Description.ToLower().Contains(searchLower)));
            }

            var schedules = await query.OrderBy(s => s.StartTime).ToListAsync();

            return schedules.Select(s => MapToDto(s, currentUserId)).ToList();
        }

        public async Task<List<ScheduleDto>> GetSharedSchedulesAsync(int currentUserId)
        {
            var schedules = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .Where(s => s.ScheduleType == "Shared" && (s.CreatedBy == currentUserId || s.Participants.Any(p => p.UserId == currentUserId)))
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return schedules.Select(s => MapToDto(s, currentUserId)).ToList();
        }

        public async Task<(bool Success, string Message, ScheduleDto? Data)> GetScheduleByIdAsync(int currentUserId, int scheduleId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null)
            {
                return (false, "Schedule not found.", null);
            }

            // Check authorization: must be creator or participant
            bool isAuthorized = schedule.CreatedBy == currentUserId || schedule.Participants.Any(p => p.UserId == currentUserId);
            if (!isAuthorized)
            {
                return (false, "You are not authorized to view this schedule.", null);
            }

            return (true, "Schedule retrieved.", MapToDto(schedule, currentUserId));
        }

        public async Task<(bool Success, string Message, ScheduleDto? Data)> UpdateScheduleAsync(int currentUserId, int scheduleId, UpdateScheduleDto dto)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null)
            {
                return (false, "Schedule not found.", null);
            }

            // Only creator can edit schedule details
            if (schedule.CreatedBy != currentUserId)
            {
                return (false, "Only the schedule creator can update this schedule.", null);
            }

            if (dto.EndTime <= dto.StartTime)
            {
                return (false, "End time must be later than start time.", null);
            }

            schedule.TaskTitle = dto.TaskTitle.Trim();
            schedule.Description = dto.Description?.Trim();
            schedule.StartTime = dto.StartTime.ToUniversalTime();
            schedule.EndTime = dto.EndTime.ToUniversalTime();
            schedule.Priority = dto.Priority;
            schedule.Status = dto.Status;
            schedule.UpdatedAt = DateTime.UtcNow;

            // If shared, update participants list if supplied
            if (schedule.ScheduleType == "Shared" && dto.ParticipantUserIds != null)
            {
                var validFriendIds = await _friendService.FilterToFriendsOnlyAsync(currentUserId, dto.ParticipantUserIds);

                // Remove participants not in the list
                var toRemove = schedule.Participants.Where(p => !validFriendIds.Contains(p.UserId)).ToList();
                foreach (var p in toRemove)
                {
                    _context.ScheduleParticipants.Remove(p);
                }

                // Add new participants
                var existingUserIds = schedule.Participants.Select(p => p.UserId).ToList();
                foreach (var friendId in validFriendIds)
                {
                    if (!existingUserIds.Contains(friendId))
                    {
                        schedule.Participants.Add(new ScheduleParticipant
                        {
                            UserId = friendId,
                            InvitationStatus = "Accepted",
                            JoinedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            var updatedDto = await MapToDtoAsync(scheduleId, currentUserId);
            return (true, "Schedule updated successfully.", updatedDto);
        }

        public async Task<(bool Success, string Message)> DeleteScheduleAsync(int currentUserId, int scheduleId)
        {
            var schedule = await _context.Schedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return (false, "Schedule not found.");
            }

            if (schedule.CreatedBy != currentUserId)
            {
                return (false, "Only the creator can delete this schedule.");
            }

            _context.Schedules.Remove(schedule);
            await _context.SaveChangesAsync();

            return (true, "Schedule deleted successfully.");
        }

        public async Task<(bool Success, string Message, ScheduleDto? Data)> MarkAsCompletedAsync(int currentUserId, int scheduleId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null)
            {
                return (false, "Schedule not found.", null);
            }

            // Both creator and accepted participants can mark a shared task as completed
            bool isAuthorized = schedule.CreatedBy == currentUserId || schedule.Participants.Any(p => p.UserId == currentUserId);
            if (!isAuthorized)
            {
                return (false, "You are not authorized to complete this schedule.", null);
            }

            if (schedule.Status == "Completed")
            {
                return (false, "Task is already marked as completed.", null);
            }

            var completingUser = await _context.Users.FindAsync(currentUserId);
            var completedAt = DateTime.UtcNow;

            schedule.Status = "Completed";
            schedule.CompletedBy = currentUserId;
            schedule.CompletedAt = completedAt;
            schedule.UpdatedAt = completedAt;

            await _context.SaveChangesAsync();

            var completedByName = completingUser?.FullName ?? "A participant";

            // If shared, notify all other participants and creator
            var notifyRecipients = new List<User>();
            if (schedule.Creator != null && schedule.Creator.UserId != currentUserId)
            {
                notifyRecipients.Add(schedule.Creator);
            }

            foreach (var participant in schedule.Participants)
            {
                if (participant.User != null && participant.UserId != currentUserId && !notifyRecipients.Any(u => u.UserId == participant.UserId))
                {
                    notifyRecipients.Add(participant.User);
                }
            }

            foreach (var recipient in notifyRecipients)
            {
                // In-app notification
                await _notificationService.CreateNotificationAsync(
                    recipient.UserId,
                    schedule.ScheduleId,
                    "TaskCompleted",
                    $"Task '{schedule.TaskTitle}' was marked as completed by {completedByName}.");

                // Send email
                try
                {
                    await _emailService.SendTaskCompletedNotificationAsync(schedule, recipient, completedByName, completedAt);
                }
                catch { }
            }

            var updatedDto = await MapToDtoAsync(scheduleId, currentUserId);
            return (true, "Task marked as completed.", updatedDto);
        }

        public async Task<(bool Success, string Message)> AddParticipantAsync(int currentUserId, int scheduleId, int friendUserId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null)
            {
                return (false, "Schedule not found.");
            }

            if (schedule.CreatedBy != currentUserId)
            {
                return (false, "Only the creator can add participants.");
            }

            if (schedule.ScheduleType != "Shared")
            {
                return (false, "Participants can only be added to shared schedules.");
            }

            bool areFriends = await _friendService.AreFriendsAsync(currentUserId, friendUserId);
            if (!areFriends)
            {
                return (false, "Only accepted friends can be invited to shared schedules.");
            }

            if (schedule.Participants.Any(p => p.UserId == friendUserId))
            {
                return (false, "This user is already a participant.");
            }

            var participant = new ScheduleParticipant
            {
                ScheduleId = scheduleId,
                UserId = friendUserId,
                InvitationStatus = "Accepted",
                JoinedAt = DateTime.UtcNow
            };

            _context.ScheduleParticipants.Add(participant);
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(currentUserId);
            var invitedUser = await _context.Users.FindAsync(friendUserId);

            if (invitedUser != null)
            {
                await _notificationService.CreateNotificationAsync(
                    invitedUser.UserId,
                    schedule.ScheduleId,
                    "ScheduleInvite",
                    $"{creator?.FullName ?? "A user"} added you to '{schedule.TaskTitle}'.");

                try
                {
                    await _emailService.SendScheduleInvitationNotificationAsync(schedule, invitedUser, creator?.FullName ?? "TimeBuddy Friend");
                }
                catch { }
            }

            return (true, "Participant added successfully.");
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int currentUserId)
        {
            var nowUtc = DateTime.UtcNow;

            var userSchedules = await _context.Schedules
                .Where(s => s.CreatedBy == currentUserId || s.Participants.Any(p => p.UserId == currentUserId))
                .ToListAsync();

            int total = userSchedules.Count;
            int completed = userSchedules.Count(s => s.Status == "Completed");
            int overdue = userSchedules.Count(s => s.Status == "Overdue" || (s.Status != "Completed" && s.Status != "Cancelled" && s.EndTime < nowUtc));
            int upcoming = userSchedules.Count(s => s.Status == "Scheduled" && s.EndTime >= nowUtc);
            int shared = userSchedules.Count(s => s.ScheduleType == "Shared");

            int pendingRequests = await _context.FriendRequests
                .CountAsync(fr => fr.ReceiverId == currentUserId && fr.Status == "Pending");

            return new DashboardSummaryDto
            {
                TotalTasks = total,
                UpcomingTasks = upcoming,
                CompletedTasks = completed,
                OverdueTasks = overdue,
                SharedTasks = shared,
                PendingFriendRequests = pendingRequests
            };
        }

        public async Task<List<ScheduleDto>> GetUpcomingTasksAsync(int currentUserId, int limit = 5)
        {
            var nowUtc = DateTime.UtcNow;

            var schedules = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .Where(s => (s.CreatedBy == currentUserId || s.Participants.Any(p => p.UserId == currentUserId))
                            && s.Status != "Completed" && s.Status != "Cancelled" && s.EndTime >= nowUtc)
                .OrderBy(s => s.EndTime)
                .Take(limit)
                .ToListAsync();

            return schedules.Select(s => MapToDto(s, currentUserId)).ToList();
        }

        public async Task<DashboardRecentDto> GetDashboardRecentAsync(int currentUserId)
        {
            var recentCompleted = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .Where(s => (s.CreatedBy == currentUserId || s.Participants.Any(p => p.UserId == currentUserId))
                            && s.Status == "Completed")
                .OrderByDescending(s => s.CompletedAt ?? s.UpdatedAt)
                .Take(5)
                .ToListAsync();

            var recentNotifications = await _notificationService.GetUserNotificationsAsync(currentUserId, 5);

            return new DashboardRecentDto
            {
                RecentlyCompletedTasks = recentCompleted.Select(s => MapToDto(s, currentUserId)).ToList(),
                RecentNotifications = recentNotifications
            };
        }

        private async Task<ScheduleDto?> MapToDtoAsync(int scheduleId, int currentUserId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.CompletedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            return schedule == null ? null : MapToDto(schedule, currentUserId);
        }

        private static ScheduleDto MapToDto(Schedule s, int currentUserId)
        {
            return new ScheduleDto
            {
                ScheduleId = s.ScheduleId,
                CreatedBy = s.CreatedBy,
                CreatorName = s.Creator?.FullName ?? "Unknown",
                CreatorEmail = s.Creator?.Email ?? "",
                IsCreator = s.CreatedBy == currentUserId,
                TaskTitle = s.TaskTitle,
                Description = s.Description,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Priority = s.Priority,
                ScheduleType = s.ScheduleType,
                Status = s.Status,
                CompletedBy = s.CompletedBy,
                CompletedByName = s.CompletedByUser?.FullName,
                CompletedAt = s.CompletedAt,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Participants = s.Participants.Select(p => new ParticipantDto
                {
                    ParticipantId = p.ParticipantId,
                    UserId = p.UserId,
                    FullName = p.User?.FullName ?? "Participant",
                    Email = p.User?.Email ?? "",
                    InvitationStatus = p.InvitationStatus,
                    JoinedAt = p.JoinedAt
                }).ToList()
            };
        }
    }
}
