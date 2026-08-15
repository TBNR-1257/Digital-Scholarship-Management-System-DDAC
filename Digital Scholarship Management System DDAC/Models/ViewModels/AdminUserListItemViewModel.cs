namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsLockedOut { get; set; }
        public string? InstitutionVerificationStatus { get; set; }
    }
}
