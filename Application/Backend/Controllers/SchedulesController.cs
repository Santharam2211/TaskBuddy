using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBuddy.Backend.DTOs;
using TimeBuddy.Backend.Services;

namespace TimeBuddy.Backend.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class SchedulesController : BaseApiController
    {
        private readonly IScheduleService _scheduleService;

        public SchedulesController(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _scheduleService.CreateScheduleAsync(CurrentUserId, dto);
            if (!success)
            {
                return BadRequest(new { message });
            }

            return CreatedAtAction(nameof(GetScheduleById), new { id = data!.ScheduleId }, new { message, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedules(
            [FromQuery] string? status,
            [FromQuery] string? priority,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? search)
        {
            var schedules = await _scheduleService.GetUserSchedulesAsync(CurrentUserId, status, priority, fromDate, toDate, search);
            return Ok(schedules);
        }

        [HttpGet("shared")]
        public async Task<IActionResult> GetSharedSchedules()
        {
            var schedules = await _scheduleService.GetSharedSchedulesAsync(CurrentUserId);
            return Ok(schedules);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetScheduleById(int id)
        {
            var (success, message, data) = await _scheduleService.GetScheduleByIdAsync(CurrentUserId, id);
            if (!success)
            {
                if (message.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message });
                }
                return NotFound(new { message });
            }

            return Ok(data);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] UpdateScheduleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _scheduleService.UpdateScheduleAsync(CurrentUserId, id, dto);
            if (!success)
            {
                if (message.Contains("Only the schedule creator", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message });
                }
                return BadRequest(new { message });
            }

            return Ok(new { message, data });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var (success, message) = await _scheduleService.DeleteScheduleAsync(CurrentUserId, id);
            if (!success)
            {
                if (message.Contains("Only the creator", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message });
                }
                return NotFound(new { message });
            }

            return Ok(new { message });
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteSchedule(int id)
        {
            var (success, message, data) = await _scheduleService.MarkAsCompletedAsync(CurrentUserId, id);
            if (!success)
            {
                if (message.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message });
                }
                return BadRequest(new { message });
            }

            return Ok(new { message, data });
        }

        [HttpPost("{id}/participants")]
        public async Task<IActionResult> AddParticipant(int id, [FromBody] AddParticipantDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message) = await _scheduleService.AddParticipantAsync(CurrentUserId, id, dto.UserId);
            if (!success)
            {
                if (message.Contains("Only the creator", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message });
                }
                return BadRequest(new { message });
            }

            return Ok(new { message });
        }
    }
}
