using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBuddy.Backend.DTOs;
using TimeBuddy.Backend.Services;

namespace TimeBuddy.Backend.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class FriendsController : BaseApiController
    {
        private readonly IFriendService _friendService;

        public FriendsController(IFriendService friendService)
        {
            _friendService = friendService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var data = await _friendService.GetFriendsAndRequestsAsync(CurrentUserId);
            return Ok(data.Friends);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetFriendRequests()
        {
            var data = await _friendService.GetFriendsAndRequestsAsync(CurrentUserId);
            return Ok(new
            {
                received = data.PendingReceivedRequests,
                sent = data.PendingSentRequests
            });
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchFriends([FromQuery] string email)
        {
            var results = await _friendService.SearchUsersByEmailAsync(email ?? "", CurrentUserId);
            return Ok(results);
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _friendService.SendFriendRequestAsync(CurrentUserId, dto.Email);
            if (!success)
            {
                return BadRequest(new { message });
            }

            return Ok(new { message, data });
        }

        [HttpPut("accept/{requestId}")]
        public async Task<IActionResult> AcceptFriendRequest(int requestId)
        {
            var (success, message) = await _friendService.AcceptFriendRequestAsync(CurrentUserId, requestId);
            if (!success)
            {
                return BadRequest(new { message });
            }

            return Ok(new { message });
        }

        [HttpPut("reject/{requestId}")]
        public async Task<IActionResult> RejectFriendRequest(int requestId)
        {
            var (success, message) = await _friendService.RejectFriendRequestAsync(CurrentUserId, requestId);
            if (!success)
            {
                return BadRequest(new { message });
            }

            return Ok(new { message });
        }

        [HttpDelete("{friendId}")]
        public async Task<IActionResult> RemoveFriend(int friendId)
        {
            var (success, message) = await _friendService.RemoveFriendAsync(CurrentUserId, friendId);
            if (!success)
            {
                return BadRequest(new { message });
            }

            return Ok(new { message });
        }
    }
}
