using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Digital_Scholarship_Management_System_DDAC.Data;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [Required]
    public string FullName { get; set; } = string.Empty;
}
