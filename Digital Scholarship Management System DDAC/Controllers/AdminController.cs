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
            var role = roles.FirstOrDefault() ?? "(none)";

            string? institutionVerificationStatus = null;
            if (role == "Provider")
            {
                var institution = await _context.InstitutionProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                institutionVerificationStatus = institution?.VerificationStatus;
            }

            list.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = role,
                IsLockedOut = await _userManager.IsLockedOutAsync(user),
                InstitutionVerificationStatus = institutionVerificationStatus
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

    public async Task<IActionResult> UserDetails(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "(none)";

        var model = new AdminUserDetailsViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = role,
            IsLockedOut = await _userManager.IsLockedOutAsync(user),
            EmailConfirmed = user.EmailConfirmed
        };

        if (role == "Student")
        {
            model.StudentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        }
        else if (role == "Provider")
        {
            model.InstitutionProfile = await _context.InstitutionProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        }

        return View(model);
    }

    public IActionResult CreateStaffUser()
    {
        return View(new CreateStaffUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaffUser(CreateStaffUserViewModel model)
    {
        if (model.Role != "Admin" && model.Role != "Moderator")
        {
            ModelState.AddModelError(nameof(model.Role), "Only Admin or Moderator accounts can be created here.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        TempData["StatusMessage"] = $"{model.Role} account created for {model.FullName} ({model.Email}).";
        return RedirectToAction(nameof(Users));
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

    // ---- Institution Activation ----

    public async Task<IActionResult> Institutions(string? statusFilter)
    {
        var query = _context.InstitutionProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(i => i.VerificationStatus == statusFilter);
        }

        var institutions = await query
            .OrderBy(i => i.VerificationStatus)
            .ThenBy(i => i.InstitutionName)
            .Select(i => new InstitutionListItemViewModel
            {
                Id = i.InstitutionProfileId,
                InstitutionName = i.InstitutionName,
                ContactEmail = i.ContactEmail,
                ContactPhone = i.ContactPhone,
                VerificationStatus = i.VerificationStatus,
                ModeratedAt = i.ModeratedAt,
                ActivatedAt = i.ActivatedAt,
                RejectionReason = i.RejectionReason
            })
            .ToListAsync();

        ViewBag.StatusFilter = statusFilter;
        return View(institutions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateInstitution(int id)
    {
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null)
        {
            return NotFound();
        }

        if (institution.VerificationStatus != "PendingAdminActivation")
        {
            TempData["StatusMessage"] = $"\"{institution.InstitutionName}\" is not awaiting admin activation.";
            return RedirectToAction(nameof(Institutions));
        }

        institution.VerificationStatus = "Active";
        institution.ActivatedByUserId = _userManager.GetUserId(User);
        institution.ActivatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"\"{institution.InstitutionName}\" has been activated.";
        return RedirectToAction(nameof(Institutions));
    }

    public async Task<IActionResult> RejectInstitution(int id)
    {
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null)
        {
            return NotFound();
        }

        var model = new RejectInstitutionViewModel
        {
            Id = institution.InstitutionProfileId,
            InstitutionName = institution.InstitutionName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectInstitution(RejectInstitutionViewModel model)
    {
        var institution = await _context.InstitutionProfiles.FindAsync(model.Id);
        if (institution == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.InstitutionName = institution.InstitutionName;
            return View(model);
        }

        institution.VerificationStatus = "Rejected";
        institution.RejectionReason = model.RejectionReason;
        institution.ActivatedByUserId = _userManager.GetUserId(User);
        institution.ActivatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"\"{institution.InstitutionName}\" was rejected.";
        return RedirectToAction(nameof(Institutions));
    }
}
