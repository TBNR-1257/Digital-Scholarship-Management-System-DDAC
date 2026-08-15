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
    private readonly IWebHostEnvironment _environment;

    public ProviderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<string> SaveUploadedFileAsync(IFormFile file)
    {
        string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return "/uploads/" + uniqueFileName;
    }

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

        if (!string.IsNullOrWhiteSpace(model.ContactEmail) &&
            await _context.InstitutionProfiles.AnyAsync(i => i.ContactEmail == model.ContactEmail))
        {
            ModelState.AddModelError(nameof(model.ContactEmail), "An institution is already registered with this contact email.");
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

        string documentPath = await SaveUploadedFileAsync(model.RegistrationDocument!);

        _context.InstitutionProfiles.Add(new InstitutionProfile
        {
            UserId = user.Id,
            InstitutionName = model.InstitutionName,
            ContactEmail = model.ContactEmail,
            ContactPhone = model.ContactPhone,
            RegistrationDocumentPath = documentPath,
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

        if (!string.IsNullOrWhiteSpace(model.ContactEmail) &&
            await _context.InstitutionProfiles.AnyAsync(i => i.ContactEmail == model.ContactEmail))
        {
            ModelState.AddModelError(nameof(model.ContactEmail), "An institution is already registered with this contact email.");
            return View(model);
        }

        string documentPath = await SaveUploadedFileAsync(model.RegistrationDocument!);

        var institution = new InstitutionProfile
        {
            UserId = CurrentUserId,
            InstitutionName = model.InstitutionName,
            ContactEmail = model.ContactEmail,
            ContactPhone = model.ContactPhone,
            RegistrationDocumentPath = documentPath,
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
            CreatedAt = DateTime.UtcNow,
            PolicyFrameworkDocumentPath = await SaveUploadedFileAsync(model.PolicyFrameworkFile!),
            EligibilityCriteriaDocumentPath = await SaveUploadedFileAsync(model.EligibilityCriteriaFile!),
            AllocationBudgetDocumentPath = await SaveUploadedFileAsync(model.AllocationBudgetFile!),
            PrivacyPolicyDocumentPath = await SaveUploadedFileAsync(model.PrivacyPolicyFile!)
        };

        _context.Scholarships.Add(scholarship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' submitted for moderator approval.";
        return RedirectToAction(nameof(Index));
    }

    // EDIT SCHOLARSHIP LISTING (GET) - only before it's live, so approved/public listings can't be silently changed.
    public async Task<IActionResult> EditScholarship(int id)
    {
        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == id && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        if (scholarship.Status != "Pending" && scholarship.Status != "Rejected")
        {
            TempData["ErrorMessage"] = "Only listings that are still pending or were rejected can be edited.";
            return RedirectToAction(nameof(Index));
        }

        var model = new ScholarshipEditViewModel
        {
            ScholarshipId = scholarship.ScholarshipId,
            Title = scholarship.Title,
            Description = scholarship.Description,
            // Rounded to 2dp - the underlying decimal columns carry extra
            // scale (from EF Core's default precision) that would otherwise
            // show up as a long string of trailing zeros in these inputs.
            MinCgpa = scholarship.MinCgpa.HasValue ? Math.Round(scholarship.MinCgpa.Value, 2) : null,
            MaxHouseholdIncome = scholarship.MaxHouseholdIncome.HasValue ? Math.Round(scholarship.MaxHouseholdIncome.Value, 2) : null,
            RequiredProgram = scholarship.RequiredProgram,
            Quota = scholarship.Quota,
            AmountPerRecipient = Math.Round(scholarship.AmountPerRecipient, 2),
            ApplicationDeadline = scholarship.ApplicationDeadline,
            CurrentPolicyFrameworkPath = scholarship.PolicyFrameworkDocumentPath,
            CurrentEligibilityCriteriaPath = scholarship.EligibilityCriteriaDocumentPath,
            CurrentAllocationBudgetPath = scholarship.AllocationBudgetDocumentPath,
            CurrentPrivacyPolicyPath = scholarship.PrivacyPolicyDocumentPath
        };

        return View(model);
    }

    // EDIT SCHOLARSHIP LISTING (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditScholarship(int id, ScholarshipEditViewModel model)
    {
        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == id && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        if (scholarship.Status != "Pending" && scholarship.Status != "Rejected")
        {
            TempData["ErrorMessage"] = "Only listings that are still pending or were rejected can be edited.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.ScholarshipId = id;
            model.CurrentPolicyFrameworkPath = scholarship.PolicyFrameworkDocumentPath;
            model.CurrentEligibilityCriteriaPath = scholarship.EligibilityCriteriaDocumentPath;
            model.CurrentAllocationBudgetPath = scholarship.AllocationBudgetDocumentPath;
            model.CurrentPrivacyPolicyPath = scholarship.PrivacyPolicyDocumentPath;
            return View(model);
        }

        scholarship.Title = model.Title;
        scholarship.Description = model.Description;
        scholarship.MinCgpa = model.MinCgpa;
        scholarship.MaxHouseholdIncome = model.MaxHouseholdIncome;
        scholarship.RequiredProgram = model.RequiredProgram;
        scholarship.Quota = model.Quota;
        scholarship.AmountPerRecipient = model.AmountPerRecipient;
        scholarship.ApplicationDeadline = model.ApplicationDeadline;

        // Only replace a document if the provider chose a new file for it.
        if (model.PolicyFrameworkFile != null)
        {
            scholarship.PolicyFrameworkDocumentPath = await SaveUploadedFileAsync(model.PolicyFrameworkFile);
        }
        if (model.EligibilityCriteriaFile != null)
        {
            scholarship.EligibilityCriteriaDocumentPath = await SaveUploadedFileAsync(model.EligibilityCriteriaFile);
        }
        if (model.AllocationBudgetFile != null)
        {
            scholarship.AllocationBudgetDocumentPath = await SaveUploadedFileAsync(model.AllocationBudgetFile);
        }
        if (model.PrivacyPolicyFile != null)
        {
            scholarship.PrivacyPolicyDocumentPath = await SaveUploadedFileAsync(model.PrivacyPolicyFile);
        }

        // Edited after a rejection - send it back to Pending for a fresh review.
        if (scholarship.Status == "Rejected")
        {
            scholarship.Status = "Pending";
            scholarship.RejectionReason = null;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' updated.";
        return RedirectToAction(nameof(Index));
    }

    // DELETE SCHOLARSHIP LISTING - only if no student has applied yet, so we never orphan an Application.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScholarship(int id)
    {
        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == id && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        bool hasApplications = await _context.Applications.AnyAsync(a => a.ScholarshipId == id);
        if (hasApplications)
        {
            TempData["ErrorMessage"] = "This listing already has applications and can't be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _context.Scholarships.Remove(scholarship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' deleted.";
        return RedirectToAction(nameof(Index));
    }

    // CLOSE/EXPIRE SCHOLARSHIP LISTING - marks a live listing as no longer accepting applications.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseScholarship(int id)
    {
        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == id && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        if (scholarship.Status != "Open")
        {
            TempData["ErrorMessage"] = "Only open listings can be closed.";
            return RedirectToAction(nameof(Index));
        }

        scholarship.Status = "Closed";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' marked as closed/expired.";
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

        var applicationRows = await (from a in _context.Applications
                                      where a.ScholarshipId == scholarshipId
                                      join u in _context.Users on a.StudentId equals u.Id
                                      join sp in _context.StudentProfiles on a.StudentId equals sp.UserId into profileGroup
                                      from sp in profileGroup.DefaultIfEmpty()
                                      orderby a.SubmittedAt
                                      select new
                                      {
                                          a.ApplicationId,
                                          a.Status,
                                          a.SubmittedAt,
                                          StudentName = sp != null ? sp.FullName : u.FullName,
                                          StudentEmail = u.Email ?? string.Empty
                                      }).ToListAsync();

        var applicationIds = applicationRows.Select(a => a.ApplicationId).ToList();
        var documents = await _context.Documents
            .Where(d => applicationIds.Contains(d.ApplicationId))
            .ToListAsync();

        var applications = applicationRows.Select(a => new ApplicationReviewViewModel
        {
            ApplicationId = a.ApplicationId,
            ScholarshipId = scholarship.ScholarshipId,
            ScholarshipTitle = scholarship.Title,
            Status = a.Status,
            SubmittedAt = a.SubmittedAt,
            StudentName = a.StudentName,
            StudentEmail = a.StudentEmail,
            Documents = documents
                .Where(d => d.ApplicationId == a.ApplicationId)
                .Select(d => new ApplicationDocumentViewModel
                {
                    DocumentId = d.DocumentId,
                    DocumentType = d.DocumentType,
                    DocumentTypeLabel = DocumentTypeCatalog.GetLabel(d.DocumentType),
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    VerificationStatus = d.VerificationStatus
                }).ToList()
        }).ToList();

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
