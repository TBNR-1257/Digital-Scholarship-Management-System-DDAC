using Microsoft.AspNetCore.Http;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class ApplicationDocumentEditViewModel
{
    public int ApplicationId { get; set; }
    public string ScholarshipTitle { get; set; } = string.Empty;

    public string? CurrentTranscriptFileName { get; set; }
    public string? CurrentTranscriptFilePath { get; set; }
    public IFormFile? TranscriptFile { get; set; }

    public string? CurrentIncomeProofFileName { get; set; }
    public string? CurrentIncomeProofFilePath { get; set; }
    public IFormFile? IncomeProofFile { get; set; }

    public string? CurrentCertificateFileName { get; set; }
    public string? CurrentCertificateFilePath { get; set; }
    public IFormFile? CertificateFile { get; set; }

    public string? CurrentIdCardFileName { get; set; }
    public string? CurrentIdCardFilePath { get; set; }
    public IFormFile? IdCardFile { get; set; }
}
