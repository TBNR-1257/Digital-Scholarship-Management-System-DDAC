using Microsoft.AspNetCore.Identity;

namespace Digital_Scholarship_Management_System_DDAC.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "Admin", "Moderator", "Student", "Provider" };

    private const string DemoPassword = "Passw0rd!";

    public static async Task SeedRolesAndDemoUsersAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureDemoUserAsync(userManager, "admin@ddac.edu", "System Admin", "Admin");
        await EnsureDemoUserAsync(userManager, "officer@ddac.edu", "Scholarship Moderator", "Moderator");
    }

    private static async Task EnsureDemoUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
