using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    public int ApplicationId { get; set; }

    [Required]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // "Pending", "Verified", "Rejected"
    public string VerificationStatus { get; set; } = "Pending";
}
