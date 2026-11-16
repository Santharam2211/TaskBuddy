using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TimeBuddy.Backend.DTOs
{
    public class CreateScheduleDto : IValidatableObject
    {
        [Required(ErrorMessage = "Task title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        public string TaskTitle { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Start time is required.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "End time is required.")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [RegularExpression("^(Low|Medium|High)$", ErrorMessage = "Priority must be Low, Medium, or High.")]
        public string Priority { get; set; } = "Medium";

        [Required(ErrorMessage = "Schedule type is required.")]
        [RegularExpression("^(Personal|Shared)$", ErrorMessage = "Schedule type must be Personal or Shared.")]
        public string ScheduleType { get; set; } = "Personal";

        public List<int> ParticipantUserIds { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
            {
                yield return new ValidationResult("End time must be later than start time.", new[] { nameof(EndTime) });
            }
        }
    }

    public class UpdateScheduleDto : IValidatableObject
    {
        [Required(ErrorMessage = "Task title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        public string TaskTitle { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Start time is required.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "End time is required.")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [RegularExpression("^(Low|Medium|High)$", ErrorMessage = "Priority must be Low, Medium, or High.")]
        public string Priority { get; set; } = "Medium";

        [Required(ErrorMessage = "Status is required.")]
        [RegularExpression("^(Scheduled|InProgress|Overdue|Completed|Cancelled)$", ErrorMessage = "Invalid status value.")]
        public string Status { get; set; } = "Scheduled";

        public List<int>? ParticipantUserIds { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
            {
                yield return new ValidationResult("End time must be later than start time.", new[] { nameof(EndTime) });
            }
        }
    }

    public class ParticipantDto
    {
        public int ParticipantId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string InvitationStatus { get; set; } = "Accepted";
        public DateTime JoinedAt { get; set; }
    }

    public class ScheduleDto
    {
        public int ScheduleId { get; set; }
        public int CreatedBy { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorEmail { get; set; } = string.Empty;
        public bool IsCreator { get; set; }

        public string TaskTitle { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string Priority { get; set; } = "Medium";
        public string ScheduleType { get; set; } = "Personal";
        public string Status { get; set; } = "Scheduled";

        public int? CompletedBy { get; set; }
        public string? CompletedByName { get; set; }
        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<ParticipantDto> Participants { get; set; } = new();
    }

    public class AddParticipantDto
    {
        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }
    }
}
