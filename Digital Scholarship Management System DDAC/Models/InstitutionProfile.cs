using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class InstitutionProfile
{
    [Key]
    public int InstitutionProfileId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? RegistrationDocumentPath { get; set; }

    // Lifecycle: "PendingModeratorReview", "PendingAdminActivation", "Active", "Rejected"
    public string VerificationStatus { get; set; } = "PendingModeratorReview";

    public string? ModeratedByUserId { get; set; } // Bryan's User ID
    public DateTime? ModeratedAt { get; set; }

    public string? ActivatedByUserId { get; set; } // Abdul's User ID
    public DateTime? ActivatedAt { get; set; }

    public string? RejectionReason { get; set; }
}
