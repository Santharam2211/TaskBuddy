using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBuddy.Backend.Services;

namespace TimeBuddy.Backend.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : BaseApiController
    {
        private readonly IScheduleService _scheduleService;

        public DashboardController(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _scheduleService.GetDashboardSummaryAsync(CurrentUserId);
            return Ok(summary);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int limit = 5)
        {
            var upcoming = await _scheduleService.GetUpcomingTasksAsync(CurrentUserId, limit);
            return Ok(upcoming);
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
        {
            var recent = await _scheduleService.GetDashboardRecentAsync(CurrentUserId);
            return Ok(recent);
        }
    }
}
