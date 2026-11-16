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
    public interface IFriendService
    {
        Task<FriendListResponseDto> GetFriendsAndRequestsAsync(int currentUserId);
        Task<List<UserDto>> SearchUsersByEmailAsync(string emailQuery, int currentUserId);
        Task<(bool Success, string Message, FriendRequestDto? Data)> SendFriendRequestAsync(int currentUserId, string targetEmail);
        Task<(bool Success, string Message)> AcceptFriendRequestAsync(int currentUserId, int requestId);
        Task<(bool Success, string Message)> RejectFriendRequestAsync(int currentUserId, int requestId);
        Task<(bool Success, string Message)> RemoveFriendAsync(int currentUserId, int friendId);
        Task<bool> AreFriendsAsync(int userAId, int userBId);
        Task<List<int>> FilterToFriendsOnlyAsync(int currentUserId, IEnumerable<int> userIds);
    }

    public class FriendService : IFriendService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public FriendService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<FriendListResponseDto> GetFriendsAndRequestsAsync(int currentUserId)
        {
            var response = new FriendListResponseDto();

            // 1. Accepted friends: requests where status = "Accepted" and user is sender or receiver
            var acceptedRequests = await _context.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr => fr.Status == "Accepted" && (fr.SenderId == currentUserId || fr.ReceiverId == currentUserId))
                .ToListAsync();

            foreach (var req in acceptedRequests)
            {
                var friendUser = req.SenderId == currentUserId ? req.Receiver : req.Sender;
                if (friendUser != null)
                {
                    response.Friends.Add(new UserDto
                    {
                        UserId = friendUser.UserId,
                        FullName = friendUser.FullName,
                        Email = friendUser.Email,
                        FriendStatus = "Friend",
                        RequestId = req.RequestId
                    });
                }
            }

            // 2. Pending received requests
            var pendingReceived = await _context.FriendRequests
                .Include(fr => fr.Sender)
                .Where(fr => fr.ReceiverId == currentUserId && fr.Status == "Pending")
                .OrderByDescending(fr => fr.CreatedAt)
                .ToListAsync();

            response.PendingReceivedRequests = pendingReceived.Select(fr => new FriendRequestDto
            {
                RequestId = fr.RequestId,
                SenderId = fr.SenderId,
                SenderName = fr.Sender?.FullName ?? "Unknown",
                SenderEmail = fr.Sender?.Email ?? "",
                ReceiverId = fr.ReceiverId,
                ReceiverName = "You",
                ReceiverEmail = "",
                Status = fr.Status,
                CreatedAt = fr.CreatedAt,
                UpdatedAt = fr.UpdatedAt
            }).ToList();

            // 3. Pending sent requests
            var pendingSent = await _context.FriendRequests
                .Include(fr => fr.Receiver)
                .Where(fr => fr.SenderId == currentUserId && fr.Status == "Pending")
                .OrderByDescending(fr => fr.CreatedAt)
                .ToListAsync();

            response.PendingSentRequests = pendingSent.Select(fr => new FriendRequestDto
            {
                RequestId = fr.RequestId,
                SenderId = fr.SenderId,
                SenderName = "You",
                SenderEmail = "",
                ReceiverId = fr.ReceiverId,
                ReceiverName = fr.Receiver?.FullName ?? "Unknown",
                ReceiverEmail = fr.Receiver?.Email ?? "",
                Status = fr.Status,
                CreatedAt = fr.CreatedAt,
                UpdatedAt = fr.UpdatedAt
            }).ToList();

            return response;
        }

        public async Task<List<UserDto>> SearchUsersByEmailAsync(string emailQuery, int currentUserId)
        {
            if (string.IsNullOrWhiteSpace(emailQuery))
            {
                return new List<UserDto>();
            }

            var query = emailQuery.Trim().ToLowerInvariant();

            // Find matching users excluding current user
            var matchedUsers = await _context.Users
                .Where(u => u.UserId != currentUserId && u.Email.ToLower().Contains(query))
                .Take(20)
                .ToListAsync();

            // Check relationship statuses
            var userIds = matchedUsers.Select(u => u.UserId).ToList();

            var relationships = await _context.FriendRequests
                .Where(fr => (fr.SenderId == currentUserId && userIds.Contains(fr.ReceiverId)) ||
                             (fr.ReceiverId == currentUserId && userIds.Contains(fr.SenderId)))
                .ToListAsync();

            var result = new List<UserDto>();

            foreach (var user in matchedUsers)
            {
                var rel = relationships.FirstOrDefault(r =>
                    (r.SenderId == currentUserId && r.ReceiverId == user.UserId) ||
                    (r.ReceiverId == currentUserId && r.SenderId == user.UserId));

                string status = "None";
                int? reqId = null;

                if (rel != null)
                {
                    reqId = rel.RequestId;
                    if (rel.Status == "Accepted")
                    {
                        status = "Friend";
                    }
                    else if (rel.Status == "Pending")
                    {
                        status = rel.SenderId == currentUserId ? "PendingSent" : "PendingReceived";
                    }
                    else if (rel.Status == "Rejected")
                    {
                        status = "None"; // Allow re-requesting after rejection
                    }
                }

                result.Add(new UserDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    FriendStatus = status,
                    RequestId = reqId
                });
            }

            return result;
        }

        public async Task<(bool Success, string Message, FriendRequestDto? Data)> SendFriendRequestAsync(int currentUserId, string targetEmail)
        {
            var normalizedEmail = targetEmail.Trim().ToLowerInvariant();

            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (targetUser == null)
            {
                return (false, "No registered user found with this email address.", null);
            }

            if (targetUser.UserId == currentUserId)
            {
                return (false, "You cannot send a friend request to yourself.", null);
            }

            // Check existing relationship
            var existingRequest = await _context.FriendRequests
                .FirstOrDefaultAsync(fr =>
                    (fr.SenderId == currentUserId && fr.ReceiverId == targetUser.UserId) ||
                    (fr.ReceiverId == currentUserId && fr.SenderId == targetUser.UserId));

            if (existingRequest != null)
            {
                if (existingRequest.Status == "Accepted")
                {
                    return (false, "You are already friends with this user.", null);
                }
                if (existingRequest.Status == "Pending")
                {
                    if (existingRequest.SenderId == currentUserId)
                    {
                        return (false, "A friend request has already been sent to this user.", null);
                    }
                    else
                    {
                        return (false, "This user has already sent you a friend request. You can accept it.", null);
                    }
                }

                // If rejected, allow resending by resetting to Pending
                existingRequest.SenderId = currentUserId;
                existingRequest.ReceiverId = targetUser.UserId;
                existingRequest.Status = "Pending";
                existingRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var sender = await _context.Users.FindAsync(currentUserId);
                await _notificationService.CreateNotificationAsync(
                    targetUser.UserId,
                    null,
                    "FriendRequest",
                    $"{sender?.FullName ?? "A user"} sent you a friend request.");

                return (true, "Friend request sent successfully.", new FriendRequestDto
                {
                    RequestId = existingRequest.RequestId,
                    SenderId = currentUserId,
                    SenderName = sender?.FullName ?? "",
                    SenderEmail = sender?.Email ?? "",
                    ReceiverId = targetUser.UserId,
                    ReceiverName = targetUser.FullName,
                    ReceiverEmail = targetUser.Email,
                    Status = "Pending",
                    CreatedAt = existingRequest.CreatedAt,
                    UpdatedAt = existingRequest.UpdatedAt
                });
            }

            var newRequest = new FriendRequest
            {
                SenderId = currentUserId,
                ReceiverId = targetUser.UserId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FriendRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            var currentSender = await _context.Users.FindAsync(currentUserId);
            await _notificationService.CreateNotificationAsync(
                targetUser.UserId,
                null,
                "FriendRequest",
                $"{currentSender?.FullName ?? "A user"} sent you a friend request.");

            return (true, "Friend request sent successfully.", new FriendRequestDto
            {
                RequestId = newRequest.RequestId,
                SenderId = currentUserId,
                SenderName = currentSender?.FullName ?? "",
                SenderEmail = currentSender?.Email ?? "",
                ReceiverId = targetUser.UserId,
                ReceiverName = targetUser.FullName,
                ReceiverEmail = targetUser.Email,
                Status = "Pending",
                CreatedAt = newRequest.CreatedAt,
                UpdatedAt = newRequest.UpdatedAt
            });
        }

        public async Task<(bool Success, string Message)> AcceptFriendRequestAsync(int currentUserId, int requestId)
        {
            var request = await _context.FriendRequests
                .Include(r => r.Receiver)
                .Include(r => r.Sender)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
            {
                return (false, "Friend request not found.");
            }

            if (request.ReceiverId != currentUserId)
            {
                return (false, "You are not authorized to accept this friend request.");
            }

            if (request.Status == "Accepted")
            {
                return (false, "This request has already been accepted.");
            }

            request.Status = "Accepted";
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify sender
            await _notificationService.CreateNotificationAsync(
                request.SenderId,
                null,
                "FriendRequestAccepted",
                $"{request.Receiver?.FullName ?? "User"} accepted your friend request.");

            return (true, "Friend request accepted.");
        }

        public async Task<(bool Success, string Message)> RejectFriendRequestAsync(int currentUserId, int requestId)
        {
            var request = await _context.FriendRequests.FindAsync(requestId);
            if (request == null)
            {
                return (false, "Friend request not found.");
            }

            if (request.ReceiverId != currentUserId)
            {
                return (false, "You are not authorized to reject this friend request.");
            }

            request.Status = "Rejected";
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return (true, "Friend request rejected.");
        }

        public async Task<(bool Success, string Message)> RemoveFriendAsync(int currentUserId, int friendId)
        {
            var relationship = await _context.FriendRequests
                .FirstOrDefaultAsync(fr =>
                    (fr.SenderId == currentUserId && fr.ReceiverId == friendId && fr.Status == "Accepted") ||
                    (fr.ReceiverId == currentUserId && fr.SenderId == friendId && fr.Status == "Accepted"));

            if (relationship == null)
            {
                return (false, "Friend relationship not found.");
            }

            _context.FriendRequests.Remove(relationship);
            await _context.SaveChangesAsync();

            return (true, "Friend removed successfully.");
        }

        public async Task<bool> AreFriendsAsync(int userAId, int userBId)
        {
            if (userAId == userBId) return true;

            return await _context.FriendRequests.AnyAsync(fr =>
                fr.Status == "Accepted" &&
                ((fr.SenderId == userAId && fr.ReceiverId == userBId) ||
                 (fr.ReceiverId == userAId && fr.SenderId == userBId)));
        }

        public async Task<List<int>> FilterToFriendsOnlyAsync(int currentUserId, IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().Where(id => id != currentUserId).ToList();
            if (!ids.Any()) return new List<int>();

            var friends = await _context.FriendRequests
                .Where(fr => fr.Status == "Accepted" &&
                    ((fr.SenderId == currentUserId && ids.Contains(fr.ReceiverId)) ||
                     (fr.ReceiverId == currentUserId && ids.Contains(fr.SenderId))))
                .Select(fr => fr.SenderId == currentUserId ? fr.ReceiverId : fr.SenderId)
                .ToListAsync();

            return friends;
        }
    }
}
