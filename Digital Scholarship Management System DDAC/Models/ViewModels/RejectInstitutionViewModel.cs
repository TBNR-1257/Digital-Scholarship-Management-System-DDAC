using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class RejectInstitutionViewModel
    {
        public int Id { get; set; }

        public string InstitutionName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide a reason for rejecting this institution.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 300 characters.")]
        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; } = string.Empty;
    }
}
