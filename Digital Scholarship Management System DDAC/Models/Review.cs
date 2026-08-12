using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class Review
{
    [Key]
    public int ReviewId { get; set; }

    [Required]
    public int ApplicationId { get; set; }

    [Required]
    public string ReviewerId { get; set; } = string.Empty;

    public decimal? Score { get; set; }
    public string? Comments { get; set; }

    // "Approve" or "Reject"
    public string? RecommendedDecision { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}
