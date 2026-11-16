using System;

namespace TimeBuddy.Backend.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FriendStatus { get; set; } // "Friend", "PendingSent", "PendingReceived", "None"
        public int? RequestId { get; set; }
    }
}
