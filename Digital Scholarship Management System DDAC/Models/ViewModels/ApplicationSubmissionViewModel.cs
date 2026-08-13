using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class ApplicationSubmissionViewModel
    {
        public int ScholarshipId { get; set; }
        public string ScholarshipTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a document type.")]
        public string DocumentType { get; set; } = "Transcript";

        [Required(ErrorMessage = "Please upload a supporting document.")]
        public IFormFile? DocumentFile { get; set; }
    }
}