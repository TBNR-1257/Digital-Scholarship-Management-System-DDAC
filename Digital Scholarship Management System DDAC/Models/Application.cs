using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class Application
{
    [Key]
    public int ApplicationId { get; set; }

    [Required]
    public int ScholarshipId { get; set; }

    [Required]
    public string StudentId { get; set; } = string.Empty;

    // "Draft", "Submitted", "UnderReview", "Approved", "Rejected", "Incomplete"
    public string Status { get; set; } = "Draft";

    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionByUserId { get; set; }
}
