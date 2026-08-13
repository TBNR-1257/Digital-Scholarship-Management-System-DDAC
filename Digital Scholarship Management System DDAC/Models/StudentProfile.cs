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

    public decimal? HouseholdIncome { get; set; }

    public string? Nationality { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string University { get; set; } = string.Empty;

    [Required]
    public decimal CurrentCGPA { get; set; }

}
