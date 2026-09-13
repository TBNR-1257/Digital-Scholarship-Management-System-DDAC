using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class Scholarship
{
    [Key]
    public int ScholarshipId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? InstitutionName { get; set; }
    public string? Description { get; set; }
    public decimal? MinCgpa { get; set; }
    public decimal? MaxHouseholdIncome { get; set; }
    public string? RequiredProgram { get; set; }
    public int Quota { get; set; }
    public decimal AmountPerRecipient { get; set; }
    public DateTime? ApplicationDeadline { get; set; }

    // "Pending", "Open", "Rejected", "Closed"
    public string Status { get; set; } = "Pending";

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ApprovedByUserId { get; set; } 
    public DateTime? DecisionAt { get; set; }
    public string? RejectionReason { get; set; }

    public string? PolicyFrameworkDocumentPath { get; set; }
    public string? EligibilityCriteriaDocumentPath { get; set; }
    public string? AllocationBudgetDocumentPath { get; set; }
    public string? PrivacyPolicyDocumentPath { get; set; }
}
