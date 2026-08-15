using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels;

public class ScholarshipEditViewModel
{
    public int ScholarshipId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please provide a description.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please specify the minimum CGPA (enter 0 if there's no requirement).")]
    [Display(Name = "Minimum CGPA")]
    [Range(0, 4.0, ErrorMessage = "CGPA must be between 0 and 4.0.")]
    public decimal? MinCgpa { get; set; }

    [Required(ErrorMessage = "Please specify the minimum household income (enter 0 if there's no requirement).")]
    [Display(Name = "Minimum Household Income (RM)")]
    [Range(0, double.MaxValue)]
    public decimal? MinHouseholdIncome { get; set; }

    [Required(ErrorMessage = "Please specify a required program, or enter 'All' if open to everyone.")]
    [Display(Name = "Required Program")]
    public string? RequiredProgram { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quota must be at least 1.")]
    public int Quota { get; set; } = 1;

    [Required]
    [Display(Name = "Amount Per Recipient (RM)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal AmountPerRecipient { get; set; }

    [Required(ErrorMessage = "Please set an application deadline.")]
    [Display(Name = "Application Deadline")]
    [DataType(DataType.Date)]
    public DateTime? ApplicationDeadline { get; set; }
}
