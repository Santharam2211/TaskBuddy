using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeBuddy.Backend.Data;
using TimeBuddy.Backend.Models;
using TimeBuddy.Backend.Services;

namespace TimeBuddy.Backend.BackgroundServices
{
    public class ScheduleMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduleMonitorService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public ScheduleMonitorService(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduleMonitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScheduleMonitorService started. Checking every {Interval} seconds.", _checkInterval.TotalSeconds);

            using var timer = new PeriodicTimer(_checkInterval);

            // Run once immediately on startup
            try
            {
                await ProcessOverdueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial check in ScheduleMonitorService failed.");
            }

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessOverdueSchedulesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when application stops
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing overdue schedules in background monitor.");
                }
            }

            _logger.LogInformation("ScheduleMonitorService stopped.");
        }

        private async Task ProcessOverdueSchedulesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var nowUtc = DateTime.UtcNow;

            // Retrieve incomplete schedules whose EndTime has passed
            // Incomplete = Status is Scheduled or InProgress (NOT Completed, NOT Cancelled)
            var dueSchedules = await context.Schedules
                .Include(s => s.Creator)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .Where(s => s.EndTime <= nowUtc && (s.Status == "Scheduled" || s.Status == "InProgress"))
                .ToListAsync(cancellationToken);

            if (!dueSchedules.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} schedules that have reached or passed their deadline.", dueSchedules.Count);

            foreach (var schedule in dueSchedules)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 1. Gather all recipients (creator + participants)
                    var recipients = new List<User>();
                    if (schedule.Creator != null)
                    {
                        recipients.Add(schedule.Creator);
                    }

                    foreach (var participant in schedule.Participants)
                    {
                        if (participant.User != null && !recipients.Any(u => u.UserId == participant.UserId))
                        {
                            recipients.Add(participant.User);
                        }
                    }

                    // 2. Dispatch notifications to all participants
                    foreach (var recipient in recipients)
                    {
                        // Check if deadline notification was already sent to this recipient
                        bool alreadyNotified = await context.EmailLogs.AnyAsync(l =>
                            l.ScheduleId == schedule.ScheduleId &&
                            l.UserId == recipient.UserId &&
                            l.NotificationType == "Deadline" &&
                            l.DeliveryStatus == "Sent",
                            cancellationToken);

                        if (!alreadyNotified)
                        {
                            // In-app notification
                            await notificationService.CreateNotificationAsync(
                                recipient.UserId,
                                schedule.ScheduleId,
                                "Deadline",
                                $"Deadline reached for task '{schedule.TaskTitle}' on {schedule.EndTime:yyyy-MM-dd HH:mm UTC}.");

                            // Email notification via Brevo
                            await emailService.SendDeadlineNotificationAsync(schedule, recipient);
                        }
                    }

                    // 3. Update status of incomplete schedule to Overdue
                    schedule.Status = "Overdue";
                    schedule.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Schedule #{ScheduleId} ('{Title}') updated to Overdue and notified.", schedule.ScheduleId, schedule.TaskTitle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to monitor/notify schedule #{ScheduleId}", schedule.ScheduleId);
                }
            }
        }
    }
}
