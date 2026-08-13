using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Users(string? search, string? roleFilter)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));
        }

        var users = query.OrderBy(u => u.FullName).ToList();
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

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            list = list.Where(u => u.Role == roleFilter).ToList();
        }

        var model = new AdminUsersPageViewModel
        {
            Users = list,
            Search = search,
            RoleFilter = roleFilter,
            AvailableRoles = _roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList()
        };

        return View(model);
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

    // ---- Scholarship Categories ----

    public async Task<IActionResult> Categories()
    {
        var categories = await _context.ScholarshipCategories.OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    public IActionResult CreateCategory()
    {
        return View("CategoryForm", new CategoryInputViewModel());
    }

    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await _context.ScholarshipCategories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var model = new CategoryInputViewModel
        {
            Id = category.ScholarshipCategoryId,
            Name = category.Name,
            Description = category.Description
        };

        return View("CategoryForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryForm(CategoryInputViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Id == 0)
        {
            _context.ScholarshipCategories.Add(new ScholarshipCategory
            {
                Name = model.Name,
                Description = model.Description
            });
            TempData["StatusMessage"] = $"Category \"{model.Name}\" was created.";
        }
        else
        {
            var category = await _context.ScholarshipCategories.FindAsync(model.Id);
            if (category == null)
            {
                return NotFound();
            }

            category.Name = model.Name;
            category.Description = model.Description;
            TempData["StatusMessage"] = $"Category \"{model.Name}\" was updated.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ScholarshipCategories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        _context.ScholarshipCategories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Category \"{category.Name}\" was deleted.";
        return RedirectToAction(nameof(Categories));
    }

    // ---- Notification Templates ----

    public async Task<IActionResult> NotificationTemplates()
    {
        var templates = await _context.NotificationTemplates.OrderBy(t => t.Name).ToListAsync();
        return View(templates);
    }

    public IActionResult CreateNotificationTemplate()
    {
        return View("NotificationTemplateForm", new NotificationTemplateInputViewModel());
    }

    public async Task<IActionResult> EditNotificationTemplate(int id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound();
        }

        var model = new NotificationTemplateInputViewModel
        {
            Id = template.NotificationTemplateId,
            Name = template.Name,
            Subject = template.Subject,
            Body = template.Body
        };

        return View("NotificationTemplateForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotificationTemplateForm(NotificationTemplateInputViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Id == 0)
        {
            _context.NotificationTemplates.Add(new NotificationTemplate
            {
                Name = model.Name,
                Subject = model.Subject,
                Body = model.Body,
                UpdatedAt = DateTime.UtcNow
            });
            TempData["StatusMessage"] = $"Notification template \"{model.Name}\" was created.";
        }
        else
        {
            var template = await _context.NotificationTemplates.FindAsync(model.Id);
            if (template == null)
            {
                return NotFound();
            }

            template.Name = model.Name;
            template.Subject = model.Subject;
            template.Body = model.Body;
            template.UpdatedAt = DateTime.UtcNow;
            TempData["StatusMessage"] = $"Notification template \"{model.Name}\" was updated.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(NotificationTemplates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotificationTemplate(int id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound();
        }

        _context.NotificationTemplates.Remove(template);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Notification template \"{template.Name}\" was deleted.";
        return RedirectToAction(nameof(NotificationTemplates));
    }
}
