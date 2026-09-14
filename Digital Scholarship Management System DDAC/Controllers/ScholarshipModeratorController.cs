using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Moderator")]
public class ScholarshipModeratorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IS3Service _s3Service;

    public ScholarshipModeratorController(
        ApplicationDbContext context,
        INotificationService notificationService,
        IS3Service s3Service)
    {
        _context = context;
        _notificationService = notificationService;
        _s3Service = s3Service;
    }

    // DASHBOARD
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingInstitutionsCount = await _context.InstitutionProfiles
            .CountAsync(i => i.VerificationStatus == "PendingModeratorReview");
        ViewBag.ApprovedInstitutionsCount = await _context.InstitutionProfiles
            .CountAsync(i => i.VerificationStatus == "PendingAdminActivation" || i.VerificationStatus == "Active");
        ViewBag.RejectedInstitutionsCount = await _context.InstitutionProfiles
            .CountAsync(i => i.VerificationStatus == "Rejected");

        ViewBag.PendingListingsCount = await _context.Scholarships
            .CountAsync(s => s.Status == "Pending");
        ViewBag.ApprovedListingsCount = await _context.Scholarships
            .CountAsync(s => s.Status == "Open" || s.Status == "Closed");
        ViewBag.RejectedListingsCount = await _context.Scholarships
            .CountAsync(s => s.Status == "Rejected");

        return View();
    }

    // ============ INSTITUTION VETTING ============

    public async Task<IActionResult> Institutions(string? search, string? statusFilter)
    {
        var query = _context.InstitutionProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(i => i.VerificationStatus == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i => i.InstitutionName.Contains(search)
                || (i.ContactEmail != null && i.ContactEmail.Contains(search)));
        }

        var institutions = await query
            .OrderBy(i => i.VerificationStatus == "PendingModeratorReview" ? 0 : 1)
            .ThenBy(i => i.InstitutionName)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.StatusFilter = statusFilter;
        return View(institutions);
    }

    public async Task<IActionResult> InstitutionDetails(int id)
    {
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null) return NotFound();

        var account = await _context.Users.FindAsync(institution.UserId);
        ViewBag.AccountEmail = account?.Email;

        if (!string.IsNullOrWhiteSpace(institution.RegistrationDocumentPath))
        {
            ViewBag.RegistrationDocumentUrl = await _s3Service.GetViewUrlAsync(institution.RegistrationDocumentPath);
        }

        return View(institution);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveInstitution(int id)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null) return NotFound();

        institution.VerificationStatus = "PendingAdminActivation";
        institution.ModeratedByUserId = currentUserId;
        institution.ModeratedAt = DateTime.UtcNow;

        string message = $"Your institution '{institution.InstitutionName}' has been approved by our moderation team and forwarded for final activation.";

        _context.Notifications.Add(new Notification
        {
            UserId = institution.UserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.PublishAsync("Institution Registration Approved", message);

        TempData["SuccessMessage"] = $"'{institution.InstitutionName}' approved and forwarded to Admin for account activation.";
        return RedirectToAction(nameof(Institutions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectInstitution(int id, string rejectionReason)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null) return NotFound();

        institution.VerificationStatus = "Rejected";
        institution.RejectionReason = rejectionReason;
        institution.ModeratedByUserId = currentUserId;
        institution.ModeratedAt = DateTime.UtcNow;

        string message = $"Your institution '{institution.InstitutionName}' registration was rejected. Reason: {rejectionReason}";

        _context.Notifications.Add(new Notification
        {
            UserId = institution.UserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.PublishAsync("Institution Registration Rejected", message);

        TempData["SuccessMessage"] = $"'{institution.InstitutionName}' has been rejected.";
        return RedirectToAction(nameof(Institutions));
    }

    // ============ SCHOLARSHIP LISTING MODERATION ============

    public async Task<IActionResult> Listings(string? search, string? statusFilter)
    {
        var query = _context.Scholarships.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(s => s.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.Title.Contains(search)
                || (s.InstitutionName != null && s.InstitutionName.Contains(search)));
        }

        var listings = await query
            .OrderBy(s => s.Status == "Pending" ? 0 : 1)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.StatusFilter = statusFilter;
        return View(listings);
    }

    public async Task<IActionResult> ListingDetails(int id)
    {
        var scholarship = await _context.Scholarships.FindAsync(id);
        if (scholarship == null) return NotFound();

        var account = await _context.Users.FindAsync(scholarship.CreatedByUserId);
        ViewBag.AccountEmail = account?.Email;

        if (!string.IsNullOrWhiteSpace(scholarship.PolicyFrameworkDocumentPath))
            ViewBag.PolicyFrameworkUrl = await _s3Service.GetViewUrlAsync(scholarship.PolicyFrameworkDocumentPath);

        if (!string.IsNullOrWhiteSpace(scholarship.EligibilityCriteriaDocumentPath))
            ViewBag.EligibilityCriteriaUrl = await _s3Service.GetViewUrlAsync(scholarship.EligibilityCriteriaDocumentPath);

        if (!string.IsNullOrWhiteSpace(scholarship.AllocationBudgetDocumentPath))
            ViewBag.AllocationBudgetUrl = await _s3Service.GetViewUrlAsync(scholarship.AllocationBudgetDocumentPath);

        if (!string.IsNullOrWhiteSpace(scholarship.PrivacyPolicyDocumentPath))
            ViewBag.PrivacyPolicyUrl = await _s3Service.GetViewUrlAsync(scholarship.PrivacyPolicyDocumentPath);

        return View(scholarship);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveListing(int id)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var scholarship = await _context.Scholarships.FindAsync(id);
        if (scholarship == null) return NotFound();

        scholarship.Status = "Open";
        scholarship.ApprovedByUserId = currentUserId;
        scholarship.DecisionAt = DateTime.UtcNow;

        string message = $"Your scholarship listing '{scholarship.Title}' has been approved and is now visible to students.";

        _context.Notifications.Add(new Notification
        {
            UserId = scholarship.CreatedByUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.PublishAsync("Scholarship Listing Approved", message);

        TempData["SuccessMessage"] = $"'{scholarship.Title}' approved and is now visible to students.";
        return RedirectToAction(nameof(Listings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectListing(int id, string rejectionReason)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var scholarship = await _context.Scholarships.FindAsync(id);
        if (scholarship == null) return NotFound();

        scholarship.Status = "Rejected";
        scholarship.RejectionReason = rejectionReason;
        scholarship.ApprovedByUserId = currentUserId;
        scholarship.DecisionAt = DateTime.UtcNow;

        string message = $"Your scholarship listing '{scholarship.Title}' was rejected. Reason: {rejectionReason}";

        _context.Notifications.Add(new Notification
        {
            UserId = scholarship.CreatedByUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.PublishAsync("Scholarship Listing Rejected", message);

        TempData["SuccessMessage"] = $"'{scholarship.Title}' has been rejected.";
        return RedirectToAction(nameof(Listings));
    }
}