using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class ApplicationSubmissionViewModel
    {
        public int ScholarshipId { get; set; }
        public string ScholarshipTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please upload your academic transcript.")]
        [Display(Name = "Academic Transcript")]
        public IFormFile? TranscriptFile { get; set; }

        [Required(ErrorMessage = "Please upload proof of household income.")]
        [Display(Name = "Proof of Household Income")]
        public IFormFile? IncomeProofFile { get; set; }

        [Required(ErrorMessage = "Please upload your achievement certificate.")]
        [Display(Name = "Achievement Certificate")]
        public IFormFile? CertificateFile { get; set; }

        [Required(ErrorMessage = "Please upload your identity card.")]
        [Display(Name = "Identity Card")]
        public IFormFile? IdCardFile { get; set; }
    }
}
