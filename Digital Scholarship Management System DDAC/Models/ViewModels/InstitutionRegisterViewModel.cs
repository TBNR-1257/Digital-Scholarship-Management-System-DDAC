using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class InstitutionRegisterViewModel
{
    [Required, MaxLength(200)]
    [Display(Name = "Institution Name")]
    public string InstitutionName { get; set; } = string.Empty;

    [EmailAddress]
    [Display(Name = "Contact Email")]
    public string? ContactEmail { get; set; }

    [Phone]
    [Display(Name = "Contact Phone")]
    public string? ContactPhone { get; set; }

    [Required(ErrorMessage = "Please upload a registration document for moderator review.")]
    [Display(Name = "Registration Document")]
    public IFormFile? RegistrationDocument { get; set; }
}
