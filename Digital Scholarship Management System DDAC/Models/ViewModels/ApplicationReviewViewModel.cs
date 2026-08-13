namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class ApplicationReviewViewModel
{
    public int ApplicationId { get; set; }
    public int ScholarshipId { get; set; }
    public string ScholarshipTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }

    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;

    public string? DocumentType { get; set; }
    public string? DocumentFileName { get; set; }
    public string? DocumentPath { get; set; }
}
