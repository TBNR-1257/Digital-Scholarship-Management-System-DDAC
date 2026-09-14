using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Digital_Scholarship_Management_System_DDAC.Services;
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
    private readonly IS3Service _s3Service;
    private readonly INotificationService _notificationService;

    public ProviderController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IS3Service s3Service,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _s3Service = s3Service;
        _notificationService = notificationService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // PUBLIC SIGN-UP: create provider account & institution profile
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

        string? documentPath = await _s3Service.UploadFileAsync(model.RegistrationDocument, "provider-documents");

        if (string.IsNullOrEmpty(documentPath))
        {
            ModelState.AddModelError(nameof(model.RegistrationDocument), "Registration document upload failed. Please attach a valid file.");
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
            await _s3Service.DeleteFileAsync(documentPath);

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
            RegistrationDocumentPath = documentPath,
            VerificationStatus = "PendingModeratorReview"
        });
        await _context.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);

        TempData["SuccessMessage"] = "Account created. Your institution is now waiting on moderator review before you can list scholarships.";
        return RedirectToAction(nameof(Index));
    }

    // DASHBOARD
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

    // INSTITUTION REGISTRATION (GET)
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

    // INSTITUTION REGISTRATION (POST)
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

        string? documentPath = await _s3Service.UploadFileAsync(model.RegistrationDocument, "provider-documents");

        if (string.IsNullOrEmpty(documentPath))
        {
            ModelState.AddModelError(nameof(model.RegistrationDocument), "Registration document upload failed. Please attach a valid file.");
            return View(model);
        }

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

    // CREATE SCHOLARSHIP LISTING (GET)
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

    // CREATE SCHOLARSHIP LISTING (POST)
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
            PolicyFrameworkDocumentPath = await _s3Service.UploadFileAsync(model.PolicyFrameworkFile, "scholarship-documents"),
            EligibilityCriteriaDocumentPath = await _s3Service.UploadFileAsync(model.EligibilityCriteriaFile, "scholarship-documents"),
            AllocationBudgetDocumentPath = await _s3Service.UploadFileAsync(model.AllocationBudgetFile, "scholarship-documents"),
            PrivacyPolicyDocumentPath = await _s3Service.UploadFileAsync(model.PrivacyPolicyFile, "scholarship-documents")
        };

        _context.Scholarships.Add(scholarship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' submitted for moderator approval.";
        return RedirectToAction(nameof(Index));
    }

    // EDIT SCHOLARSHIP LISTING (GET)
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
            MinCgpa = scholarship.MinCgpa.HasValue ? Math.Round(scholarship.MinCgpa.Value, 2) : null,
            MaxHouseholdIncome = scholarship.MaxHouseholdIncome.HasValue ? Math.Round(scholarship.MaxHouseholdIncome.Value, 2) : null,
            RequiredProgram = scholarship.RequiredProgram,
            Quota = scholarship.Quota,
            AmountPerRecipient = Math.Round(scholarship.AmountPerRecipient, 2),
            ApplicationDeadline = scholarship.ApplicationDeadline,
            CurrentPolicyFrameworkPath = await _s3Service.GetViewUrlAsync(scholarship.PolicyFrameworkDocumentPath) ?? scholarship.PolicyFrameworkDocumentPath,
            CurrentEligibilityCriteriaPath = await _s3Service.GetViewUrlAsync(scholarship.EligibilityCriteriaDocumentPath) ?? scholarship.EligibilityCriteriaDocumentPath,
            CurrentAllocationBudgetPath = await _s3Service.GetViewUrlAsync(scholarship.AllocationBudgetDocumentPath) ?? scholarship.AllocationBudgetDocumentPath,
            CurrentPrivacyPolicyPath = await _s3Service.GetViewUrlAsync(scholarship.PrivacyPolicyDocumentPath) ?? scholarship.PrivacyPolicyDocumentPath
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
            model.CurrentPolicyFrameworkPath = await _s3Service.GetViewUrlAsync(scholarship.PolicyFrameworkDocumentPath) ?? scholarship.PolicyFrameworkDocumentPath;
            model.CurrentEligibilityCriteriaPath = await _s3Service.GetViewUrlAsync(scholarship.EligibilityCriteriaDocumentPath) ?? scholarship.EligibilityCriteriaDocumentPath;
            model.CurrentAllocationBudgetPath = await _s3Service.GetViewUrlAsync(scholarship.AllocationBudgetDocumentPath) ?? scholarship.AllocationBudgetDocumentPath;
            model.CurrentPrivacyPolicyPath = await _s3Service.GetViewUrlAsync(scholarship.PrivacyPolicyDocumentPath) ?? scholarship.PrivacyPolicyDocumentPath;
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

        if (model.PolicyFrameworkFile != null)
        {
            if (!string.IsNullOrEmpty(scholarship.PolicyFrameworkDocumentPath))
            {
                await _s3Service.DeleteFileAsync(scholarship.PolicyFrameworkDocumentPath);
            }
            scholarship.PolicyFrameworkDocumentPath = await _s3Service.UploadFileAsync(model.PolicyFrameworkFile, "scholarship-documents");
        }
        if (model.EligibilityCriteriaFile != null)
        {
            if (!string.IsNullOrEmpty(scholarship.EligibilityCriteriaDocumentPath))
            {
                await _s3Service.DeleteFileAsync(scholarship.EligibilityCriteriaDocumentPath);
            }
            scholarship.EligibilityCriteriaDocumentPath = await _s3Service.UploadFileAsync(model.EligibilityCriteriaFile, "scholarship-documents");
        }
        if (model.AllocationBudgetFile != null)
        {
            if (!string.IsNullOrEmpty(scholarship.AllocationBudgetDocumentPath))
            {
                await _s3Service.DeleteFileAsync(scholarship.AllocationBudgetDocumentPath);
            }
            scholarship.AllocationBudgetDocumentPath = await _s3Service.UploadFileAsync(model.AllocationBudgetFile, "scholarship-documents");
        }
        if (model.PrivacyPolicyFile != null)
        {
            if (!string.IsNullOrEmpty(scholarship.PrivacyPolicyDocumentPath))
            {
                await _s3Service.DeleteFileAsync(scholarship.PrivacyPolicyDocumentPath);
            }
            scholarship.PrivacyPolicyDocumentPath = await _s3Service.UploadFileAsync(model.PrivacyPolicyFile, "scholarship-documents");
        }

        if (scholarship.Status == "Rejected")
        {
            scholarship.Status = "Pending";
            scholarship.RejectionReason = null;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' updated.";
        return RedirectToAction(nameof(Index));
    }

    // DELETE SCHOLARSHIP LISTING
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

        if (!string.IsNullOrEmpty(scholarship.PolicyFrameworkDocumentPath))
            await _s3Service.DeleteFileAsync(scholarship.PolicyFrameworkDocumentPath);
        if (!string.IsNullOrEmpty(scholarship.EligibilityCriteriaDocumentPath))
            await _s3Service.DeleteFileAsync(scholarship.EligibilityCriteriaDocumentPath);
        if (!string.IsNullOrEmpty(scholarship.AllocationBudgetDocumentPath))
            await _s3Service.DeleteFileAsync(scholarship.AllocationBudgetDocumentPath);
        if (!string.IsNullOrEmpty(scholarship.PrivacyPolicyDocumentPath))
            await _s3Service.DeleteFileAsync(scholarship.PrivacyPolicyDocumentPath);

        _context.Scholarships.Remove(scholarship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"'{scholarship.Title}' deleted.";
        return RedirectToAction(nameof(Index));
    }

    // CLOSE SCHOLARSHIP LISTING
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

    // VIEW APPLICATIONS (Accepts either 'id' or 'scholarshipId' from routing)
    public async Task<IActionResult> Applications(int? id, int? scholarshipId)
    {
        int targetScholarshipId = id ?? scholarshipId ?? 0;

        if (targetScholarshipId == 0)
        {
            return NotFound();
        }

        var scholarship = await _context.Scholarships
            .FirstOrDefaultAsync(s => s.ScholarshipId == targetScholarshipId && s.CreatedByUserId == CurrentUserId);

        if (scholarship == null)
        {
            return NotFound();
        }

        ViewBag.ScholarshipTitle = scholarship.Title;
        ViewBag.ScholarshipId = scholarship.ScholarshipId;

        var applicationsList = await _context.Applications
            .Where(a => a.ScholarshipId == targetScholarshipId &&
                        a.Status != null &&
                        a.Status.ToLower() != "draft")
            .OrderByDescending(a => a.SubmittedAt ?? DateTime.MinValue)
            .ToListAsync();

        if (!applicationsList.Any())
        {
            return View(new List<ApplicationReviewViewModel>());
        }

        var studentIds = applicationsList.Select(a => a.StudentId).Distinct().ToList();

        var users = await _context.Users
            .Where(u => studentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var studentProfiles = await _context.StudentProfiles
            .Where(sp => studentIds.Contains(sp.UserId))
            .ToDictionaryAsync(sp => sp.UserId);

        var applicationIds = applicationsList.Select(a => a.ApplicationId).ToList();
        var documents = await _context.Documents
            .Where(d => applicationIds.Contains(d.ApplicationId))
            .ToListAsync();

        var applicationViewModels = new List<ApplicationReviewViewModel>();

        foreach (var app in applicationsList)
        {
            users.TryGetValue(app.StudentId, out var user);
            studentProfiles.TryGetValue(app.StudentId, out var profile);

            var appDocs = documents.Where(d => d.ApplicationId == app.ApplicationId).ToList();
            var docViewModels = new List<ApplicationDocumentViewModel>();

            foreach (var doc in appDocs)
            {
                docViewModels.Add(new ApplicationDocumentViewModel
                {
                    DocumentId = doc.DocumentId,
                    DocumentType = doc.DocumentType,
                    DocumentTypeLabel = DocumentTypeCatalog.GetLabel(doc.DocumentType),
                    FileName = doc.FileName,
                    FilePath = await GetAccessibleDocumentUrlAsync(doc.FilePath),
                    VerificationStatus = doc.VerificationStatus
                });
            }

            applicationViewModels.Add(new ApplicationReviewViewModel
            {
                ApplicationId = app.ApplicationId,
                ScholarshipId = scholarship.ScholarshipId,
                ScholarshipTitle = scholarship.Title,
                Status = app.Status,
                SubmittedAt = app.SubmittedAt,
                StudentName = !string.IsNullOrWhiteSpace(profile?.FullName) ? profile.FullName : (!string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : "Student"),
                StudentEmail = user?.Email ?? string.Empty,
                Documents = docViewModels
            });
        }

        return View(applicationViewModels);
    }

    // HELPER: Safely returns S3 URL or fallback path
    private async Task<string> GetAccessibleDocumentUrlAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "#";

        var viewUrl = await _s3Service.GetViewUrlAsync(filePath);
        return viewUrl ?? filePath;
    }

    // DECIDE ON APPLICATION
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

        // Fire-and-forget style call to the SNS-backed notification microservice.
        // PublishAsync swallows its own exceptions, so a failure here never blocks the decision above.
        await _notificationService.PublishAsync(
            $"Scholarship Application {decision}",
            message);

        TempData["SuccessMessage"] = $"Application {decision.ToLower()} and student notified.";
        return RedirectToAction(nameof(Applications), new { scholarshipId = scholarship.ScholarshipId });
    }

}