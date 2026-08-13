using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Provider")]
public class ProviderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ProviderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // PUBLIC SIGN-UP: anyone can apply to become a Scholarship Provider.
    // Creates the login account (Provider role) AND the institution profile
    // together, both starting in a pending state until Moderator + Admin
    // approve. Unlike the site's default Register page (which always makes
    // Student accounts), this is the dedicated entry point for institutions.
    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignUp()
    {
        return View(new ProviderSignUpViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(ProviderSignUpViewModel model)
    {
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
            FullName = model.InstitutionName,
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

        await _userManager.AddToRoleAsync(user, "Provider");

        _context.InstitutionProfiles.Add(new InstitutionProfile
        {
            UserId = user.Id,
            InstitutionName = model.InstitutionName,
            ContactEmail = model.ContactEmail,
            ContactPhone = model.ContactPhone,
            VerificationStatus = "PendingModeratorReview"
        });
        await _context.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);

        TempData["SuccessMessage"] = "Account created. Your institution is now waiting on moderator review before you can list scholarships.";
        return RedirectToAction(nameof(Index));
    }

    // DASHBOARD: institution status + quick links + a summary of your scholarships.
    public async Task<IActionResult> Index()
    {
        var institution = await _context.InstitutionProfiles
            .FirstOrDefaultAsync(i => i.UserId == CurrentUserId);

        if (institution == null)
        {
            return RedirectToAction(nameof(Register));
        }

        ViewBag.Institution = institution;

        var scholarships = await _context.Scholarships
            .Where(s => s.CreatedByUserId == CurrentUserId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View(scholarships);
    }

    // 1. INSTITUTION REGISTRATION (GET)
    public async Task<IActionResult> Register()
    {
        var existing = await _context.InstitutionProfiles
            .FirstOrDefaultAsync(i => i.UserId == CurrentUserId);

        if (existing != null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new InstitutionRegisterViewModel());
    }

    // 1. INSTITUTION REGISTRATION (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(InstitutionRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _context.InstitutionProfiles
            .AnyAsync(i => i.UserId == CurrentUserId);

        if (existing)
        {
            return RedirectToAction(nameof(Index));
        }

        var institution = new InstitutionProfile
        {
            UserId = CurrentUserId,
            InstitutionName = model.InstitutionName,
            ContactEmail = model.ContactEmail,
            ContactPhone = model.ContactPhone,
            VerificationStatus = "PendingModeratorReview"
        };

        _context.InstitutionProfiles.Add(institution);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Institution registered. Waiting on moderator review before you can list scholarships.";
        return RedirectToAction(nameof(Index));
    }

    // 2. CREATE SCHOLARSHIP LISTING (GET)
    public async Task<IActionResult> CreateScholarship()
    {
        var institution = await _context.InstitutionProfiles
            .FirstOrDefaultAsync(i => i.UserId == CurrentUserId);

        if (institution == null)
        {
            return RedirectToAction(nameof(Register));
        }

        if (institution.VerificationStatus != "Active")
        {
            TempData["ErrorMessage"] = "Your institution must be verified and activated before you can create scholarship listings.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ScholarshipCreateViewModel());
    }

    // 2. CREATE SCHOLARSHIP LISTING (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateScholarship(ScholarshipCreateViewModel model)
    {
        var institution = await _context.InstitutionProfiles
            .FirstOrDefaultAsync(i => i.UserId == CurrentUserId);

        if (institution == null)
        {
            return RedirectToAction(nameof(Register));
        }

        if (institution.VerificationStatus != "Active")
        {
            TempData["ErrorMessage"] = "Your institution must be verified and activated before you can create scholarship listings.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var scholarship = new Scholarship
        {
            Title = model.Title,
            InstitutionName = institution.InstitutionName,
            Description = model.Description,
            MinCgpa = model.MinCgpa,
            MaxHouseholdIncome = model.MaxHouseholdIncome,
            RequiredProgram = model.RequiredProgram,
            Quota = model.Quota,
            AmountPerRecipient = model.AmountPerRecipient,
            ApplicationDeadline = model.ApplicationDeadline,
            Status = "Pending",
            CreatedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Scholarships.Add(scholarship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' submitted for moderator approval.";
        return RedirectToAction(nameof(Index));
    }

    // 3. VIEW APPLICATIONS for one of your scholarships
    public async Task<IActionResult> Applications(int scholarshipId)
    {
        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == scholarshipId && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        var applications = await (from a in _context.Applications
                                   where a.ScholarshipId == scholarshipId
                                   join u in _context.Users on a.StudentId equals u.Id
                                   join sp in _context.StudentProfiles on a.StudentId equals sp.UserId into profileGroup
                                   from sp in profileGroup.DefaultIfEmpty()
                                   join doc in _context.Documents on a.ApplicationId equals doc.ApplicationId into docGroup
                                   from doc in docGroup.DefaultIfEmpty()
                                   orderby a.SubmittedAt
                                   select new ApplicationReviewViewModel
                                   {
                                       ApplicationId = a.ApplicationId,
                                       ScholarshipId = scholarship.ScholarshipId,
                                       ScholarshipTitle = scholarship.Title,
                                       Status = a.Status,
                                       SubmittedAt = a.SubmittedAt,
                                       StudentName = sp != null ? sp.FullName : u.FullName,
                                       StudentEmail = u.Email ?? string.Empty,
                                       DocumentType = doc != null ? doc.DocumentType : null,
                                       DocumentFileName = doc != null ? doc.FileName : null,
                                       DocumentPath = doc != null ? doc.FilePath : null
                                   }).ToListAsync();

        ViewBag.ScholarshipTitle = scholarship.Title;
        ViewBag.ScholarshipId = scholarship.ScholarshipId;

        return View(applications);
    }

    // 3. APPROVE/REJECT an application, notifying the student.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int applicationId, string decision, string? reason)
    {
        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null)
        {
            return NotFound();
        }

        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == application.ScholarshipId && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            // Not your scholarship - not authorized to decide on it.
            return Forbid();
        }

        if (decision != "Approved" && decision != "Rejected")
        {
            return BadRequest();
        }

        if (decision == "Rejected" && string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Please provide a reason when rejecting an application.";
            return RedirectToAction(nameof(Applications), new { scholarshipId = scholarship.ScholarshipId });
        }

        application.Status = decision;
        application.DecisionAt = DateTime.UtcNow;
        application.DecisionByUserId = CurrentUserId;

        string message = decision == "Approved"
            ? $"Your application for '{scholarship.Title}' has been approved."
            : $"Your application for '{scholarship.Title}' has been rejected." +
              (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason}");

        _context.Notifications.Add(new Notification
        {
            UserId = application.StudentId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Application {decision.ToLower()} and student notified.";
        return RedirectToAction(nameof(Applications), new { scholarshipId = scholarship.ScholarshipId });
    }
}
