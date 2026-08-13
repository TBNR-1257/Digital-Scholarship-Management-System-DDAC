namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class AdminUsersPageViewModel
    {
        public List<AdminUserListItemViewModel> Users { get; set; } = new();
        public string? Search { get; set; }
        public string? RoleFilter { get; set; }
        public List<string> AvailableRoles { get; set; } = new();
    }
}
