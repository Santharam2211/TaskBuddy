using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBuddy.Backend.Services;

namespace TimeBuddy.Backend.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : BaseApiController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int limit = 50)
        {
            var notifications = await _notificationService.GetUserNotificationsAsync(CurrentUserId, limit);
            var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);
            return Ok(new { notifications, unreadCount });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var success = await _notificationService.MarkAsReadAsync(CurrentUserId, id);
            if (!success)
            {
                return NotFound(new { message = "Notification not found." });
            }

            return Ok(new { message = "Notification marked as read." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync(CurrentUserId);
            return Ok(new { message = "All notifications marked as read." });
        }
    }
}
