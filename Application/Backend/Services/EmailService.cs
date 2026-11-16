using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using TimeBuddy.Backend.Data;
using TimeBuddy.Backend.Models;

namespace TimeBuddy.Backend.Services
{
    public interface IEmailService
    {
        Task<bool> SendDeadlineNotificationAsync(Schedule schedule, User recipient);
        Task<bool> SendTaskCompletedNotificationAsync(Schedule schedule, User recipient, string completedByName, DateTime completedAt);
        Task<bool> SendScheduleInvitationNotificationAsync(Schedule schedule, User recipient, string creatorName);
        Task<bool> SendCustomEmailAsync(string toEmail, string toName, string subject, string htmlContent, int? scheduleId = null, int? userId = null, string notificationType = "Generic");
    }

    public class EmailService : IEmailService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<EmailService> logger,
            HttpClient httpClient)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<bool> SendDeadlineNotificationAsync(Schedule schedule, User recipient)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Idempotency check: Has this deadline notification already been sent to this user for this schedule?
            bool alreadySent = await context.EmailLogs.AnyAsync(l =>
                l.ScheduleId == schedule.ScheduleId &&
                l.UserId == recipient.UserId &&
                l.NotificationType == "Deadline" &&
                l.DeliveryStatus == "Sent");

            if (alreadySent)
            {
                _logger.LogInformation("Deadline email already sent to {Email} for schedule #{ScheduleId}. Skipping.", recipient.Email, schedule.ScheduleId);
                return true;
            }

            var subject = $"[Deadline Alert] Task '{schedule.TaskTitle}' has reached its deadline";
            var deadlineFormatted = schedule.EndTime.ToString("yyyy-MM-dd HH:mm UTC");

            var htmlContent = GetEmailTemplate(
                heading: "Task Deadline Alert",
                introText: $"The deadline for your task <strong>{EscapeHtml(schedule.TaskTitle)}</strong> has been reached.",
                details: new Dictionary<string, string>
                {
                    { "Task Title", EscapeHtml(schedule.TaskTitle) },
                    { "Description", string.IsNullOrWhiteSpace(schedule.Description) ? "No description provided." : EscapeHtml(schedule.Description) },
                    { "Priority", schedule.Priority },
                    { "Scheduled Deadline", deadlineFormatted },
                    { "Status", "Overdue / Incomplete" }
                },
                callToAction: "Please log in to TimeBuddy to complete or reschedule your task."
            );

            return await SendAndLogEmailAsync(recipient.Email, recipient.FullName, subject, htmlContent, schedule.ScheduleId, recipient.UserId, "Deadline");
        }

        public async Task<bool> SendTaskCompletedNotificationAsync(Schedule schedule, User recipient, string completedByName, DateTime completedAt)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Idempotency check
            bool alreadySent = await context.EmailLogs.AnyAsync(l =>
                l.ScheduleId == schedule.ScheduleId &&
                l.UserId == recipient.UserId &&
                l.NotificationType == "TaskCompleted" &&
                l.DeliveryStatus == "Sent");

            if (alreadySent)
            {
                return true;
            }

            var subject = $"[Task Completed] '{schedule.TaskTitle}' was completed by {completedByName}";
            var completedFormatted = completedAt.ToString("yyyy-MM-dd HH:mm UTC");

            var htmlContent = GetEmailTemplate(
                heading: "Task Completed 🎉",
                introText: $"Good news! The shared task <strong>{EscapeHtml(schedule.TaskTitle)}</strong> has been marked as completed.",
                details: new Dictionary<string, string>
                {
                    { "Task Title", EscapeHtml(schedule.TaskTitle) },
                    { "Completed By", EscapeHtml(completedByName) },
                    { "Completion Time", completedFormatted },
                    { "Priority", schedule.Priority }
                },
                callToAction: "View this task and review your updated schedule on TimeBuddy."
            );

            return await SendAndLogEmailAsync(recipient.Email, recipient.FullName, subject, htmlContent, schedule.ScheduleId, recipient.UserId, "TaskCompleted");
        }

        public async Task<bool> SendScheduleInvitationNotificationAsync(Schedule schedule, User recipient, string creatorName)
        {
            var subject = $"[Shared Schedule] {creatorName} invited you to collaborate on '{schedule.TaskTitle}'";
            var startFormatted = schedule.StartTime.ToString("yyyy-MM-dd HH:mm UTC");
            var endFormatted = schedule.EndTime.ToString("yyyy-MM-dd HH:mm UTC");

            var htmlContent = GetEmailTemplate(
                heading: "Collaboration Invitation",
                introText: $"<strong>{EscapeHtml(creatorName)}</strong> has invited you to collaborate on a shared schedule.",
                details: new Dictionary<string, string>
                {
                    { "Task Title", EscapeHtml(schedule.TaskTitle) },
                    { "Description", string.IsNullOrWhiteSpace(schedule.Description) ? "No description provided." : EscapeHtml(schedule.Description) },
                    { "Start Time", startFormatted },
                    { "End Time", endFormatted },
                    { "Priority", schedule.Priority }
                },
                callToAction: "Open TimeBuddy to view task details and coordinate with your team."
            );

            return await SendAndLogEmailAsync(recipient.Email, recipient.FullName, subject, htmlContent, schedule.ScheduleId, recipient.UserId, "Invitation");
        }

        public async Task<bool> SendCustomEmailAsync(string toEmail, string toName, string subject, string htmlContent, int? scheduleId = null, int? userId = null, string notificationType = "Generic")
        {
            return await SendAndLogEmailAsync(toEmail, toName, subject, htmlContent, scheduleId, userId, notificationType);
        }

        private async Task<bool> SendAndLogEmailAsync(
            string toEmail,
            string toName,
            string subject,
            string htmlContent,
            int? scheduleId,
            int? userId,
            string notificationType)
        {
            bool isSuccess = false;
            string? errorMessage = null;

            var brevoConfig = _configuration.GetSection("Brevo");
            var apiKey = brevoConfig["ApiKey"];
            var senderEmail = brevoConfig["SenderEmail"] ?? "santharamsenthilkumar17@gmail.com";
            var senderName = brevoConfig["SenderName"] ?? "TimeBuddy";
            bool useHttpApi = bool.TryParse(brevoConfig["UseHttpApi"], out bool u) ? u : true;

            try
            {
                if (useHttpApi && !string.IsNullOrWhiteSpace(apiKey))
                {
                    isSuccess = await SendViaBrevoApiAsync(apiKey, senderEmail, senderName, toEmail, toName, subject, htmlContent);
                }
                else
                {
                    isSuccess = await SendViaSmtpAsync(senderEmail, senderName, toEmail, toName, subject, htmlContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail} for {NotificationType}", toEmail, notificationType);
                errorMessage = ex.Message;
                isSuccess = false;
            }

            // Persist log using independent scope
            if (userId.HasValue)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var logEntry = new EmailLog
                    {
                        ScheduleId = scheduleId,
                        UserId = userId.Value,
                        NotificationType = notificationType,
                        DeliveryStatus = isSuccess ? "Sent" : "Failed",
                        SentAt = DateTime.UtcNow,
                        ErrorMessage = errorMessage
                    };

                    dbContext.EmailLogs.Add(logEntry);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Could not save EmailLog for user {UserId}", userId.Value);
                }
            }

            return isSuccess;
        }

        private async Task<bool> SendViaBrevoApiAsync(
            string apiKey,
            string senderEmail,
            string senderName,
            string toEmail,
            string toName,
            string subject,
            string htmlContent)
        {
            var url = "https://api.brevo.com/v3/smtp/email";

            var payload = new
            {
                sender = new { name = senderName, email = senderEmail },
                to = new[] { new { email = toEmail, name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName } },
                subject = subject,
                htmlContent = htmlContent
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully dispatched email via Brevo API to {ToEmail}", toEmail);
                return true;
            }
            else
            {
                var respBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Brevo API error ({StatusCode}): {Body}. Falling back to SMTP...", response.StatusCode, respBody);
                return await SendViaSmtpAsync(senderEmail, senderName, toEmail, toName, subject, htmlContent);
            }
        }

        private async Task<bool> SendViaSmtpAsync(
            string senderEmail,
            string senderName,
            string toEmail,
            string toName,
            string subject,
            string htmlContent)
        {
            var brevoConfig = _configuration.GetSection("Brevo");
            var smtpHost = brevoConfig["SmtpHost"] ?? "smtp-relay.brevo.com";
            var smtpPort = int.TryParse(brevoConfig["SmtpPort"], out int p) ? p : 587;
            var smtpUser = brevoConfig["SmtpUser"] ?? "";
            var smtpPass = brevoConfig["SmtpPass"] ?? "";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlContent };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
            {
                await client.AuthenticateAsync(smtpUser, smtpPass);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Successfully sent email via SMTP to {ToEmail}", toEmail);
            return true;
        }

        private string GetEmailTemplate(string heading, string introText, Dictionary<string, string> details, string callToAction)
        {
            var rowsBuilder = new StringBuilder();
            foreach (var item in details)
            {
                rowsBuilder.Append($@"
                    <tr>
                        <td style='padding: 8px 12px; font-weight: 600; color: #4b5563; border-bottom: 1px solid #f3f4f6; width: 35%;'>
                            {item.Key}
                        </td>
                        <td style='padding: 8px 12px; color: #1f2937; border-bottom: 1px solid #f3f4f6;'>
                            {item.Value}
                        </td>
                    </tr>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>TimeBuddy Notification</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f3f4f6; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color: #f3f4f6; padding: 30px 10px;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' max-width='600' cellspacing='0' cellpadding='0' border='0' style='max-width: 600px; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%); padding: 24px 32px; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.5px;'>⏱️ TimeBuddy</h1>
                            <p style='margin: 4px 0 0; font-size: 13px; opacity: 0.9;'>Collaborative Task Scheduling & Notifications</p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style='padding: 32px;'>
                            <h2 style='margin: 0 0 12px; font-size: 18px; color: #111827;'>{heading}</h2>
                            <p style='margin: 0 0 20px; font-size: 14px; line-height: 1.5; color: #4b5563;'>
                                {introText}
                            </p>

                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color: #f9fafb; border-radius: 8px; border: 1px solid #e5e7eb; margin-bottom: 24px; font-size: 13px;'>
                                {rowsBuilder}
                            </table>

                            <div style='background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; border-radius: 4px; margin-bottom: 24px;'>
                                <p style='margin: 0; font-size: 13px; color: #1e40af;'>
                                    📌 {callToAction}
                                </p>
                            </div>

                            <p style='margin: 0; font-size: 12px; color: #9ca3af; text-align: center;'>
                                This is an automated notification from <strong>TimeBuddy</strong>. Please do not reply directly to this email.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f9fafb; padding: 16px 32px; text-align: center; font-size: 12px; color: #6b7280; border-top: 1px solid #e5e7eb;'>
                            &copy; {DateTime.UtcNow.Year} TimeBuddy System. All rights reserved.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static string EscapeHtml(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(text);
        }
    }
}
