namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class ApplicationDocumentViewModel
{
    public int DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentTypeLabel { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = "Pending";
}
