using Microsoft.AspNetCore.Identity;

namespace Digital_Scholarship_Management_System_DDAC.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string? FullName { get; set; }
}
