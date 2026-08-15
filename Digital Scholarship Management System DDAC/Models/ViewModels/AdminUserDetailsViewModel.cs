using Digital_Scholarship_Management_System_DDAC.Models;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class AdminUserDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsLockedOut { get; set; }
        public bool EmailConfirmed { get; set; }

        public StudentProfile? StudentProfile { get; set; }
        public InstitutionProfile? InstitutionProfile { get; set; }
    }
}
