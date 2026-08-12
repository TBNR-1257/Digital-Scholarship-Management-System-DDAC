using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models;

public class StudentProfile
{
    [Key]
    public int StudentProfileId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? ProgramOfStudy { get; set; }
    public int? YearOfStudy { get; set; }
    public decimal? Cgpa { get; set; }
    public decimal? HouseholdIncome { get; set; }
    public string? Nationality { get; set; }
}
