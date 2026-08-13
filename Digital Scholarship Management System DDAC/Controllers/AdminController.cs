using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Users()
    {
        var users = _userManager.Users.OrderBy(u => u.FullName).ToList();
        var list = new List<AdminUserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            list.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "(none)",
                IsLockedOut = await _userManager.IsLockedOutAsync(user)
            });
        }

        return View(list);
    }

    public async Task<IActionResult> EditUserRole(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        var model = new EditUserRoleViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            SelectedRole = currentRoles.FirstOrDefault() ?? string.Empty,
            AvailableRoles = _roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUserRole(EditUserRoleViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.AvailableRoles = _roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
            return View(model);
        }

        if (user.Id == _userManager.GetUserId(User) && model.SelectedRole != "Admin")
        {
            ModelState.AddModelError(string.Empty, "You cannot remove your own Admin role.");
            model.AvailableRoles = _roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.SelectedRole);

        TempData["StatusMessage"] = $"{user.FullName}'s role was updated to {model.SelectedRole}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLockout(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["StatusMessage"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Users));
        }

        var isLockedOut = await _userManager.IsLockedOutAsync(user);
        if (isLockedOut)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            TempData["StatusMessage"] = $"{user.FullName}'s account has been re-enabled.";
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            TempData["StatusMessage"] = $"{user.FullName}'s account has been disabled.";
        }

        return RedirectToAction(nameof(Users));
    }
}
