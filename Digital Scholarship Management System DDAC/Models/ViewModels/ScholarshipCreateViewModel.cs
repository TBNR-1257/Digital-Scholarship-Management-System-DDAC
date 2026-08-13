using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class ScholarshipCreateViewModel
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Display(Name = "Minimum CGPA")]
    [Range(0, 4.0, ErrorMessage = "CGPA must be between 0 and 4.0.")]
    public decimal? MinCgpa { get; set; }

    [Display(Name = "Maximum Household Income")]
    public decimal? MaxHouseholdIncome { get; set; }

    [Display(Name = "Required Program (leave blank or 'All' for any)")]
    public string? RequiredProgram { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quota must be at least 1.")]
    public int Quota { get; set; } = 1;

    [Display(Name = "Amount Per Recipient (RM)")]
    [Range(0, double.MaxValue)]
    public decimal AmountPerRecipient { get; set; }

    [Display(Name = "Application Deadline")]
    [DataType(DataType.Date)]
    public DateTime? ApplicationDeadline { get; set; }
}
