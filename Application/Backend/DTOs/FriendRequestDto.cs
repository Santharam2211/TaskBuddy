using System;
using System.ComponentModel.DataAnnotations;

namespace TimeBuddy.Backend.DTOs
{
    public class FriendRequestDto
    {
        public int RequestId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;

        public int ReceiverId { get; set; }
        public string ReceiverName { get; set; } = string.Empty;
        public string ReceiverEmail { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SendFriendRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Valid email is required.")]
        public string Email { get; set; } = string.Empty;
    }

    public class FriendListResponseDto
    {
        public List<UserDto> Friends { get; set; } = new();
        public List<FriendRequestDto> PendingReceivedRequests { get; set; } = new();
        public List<FriendRequestDto> PendingSentRequests { get; set; } = new();
    }
}
