using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Moderator")]
public class ScholarshipModeratorController : Controller
{
    private readonly ApplicationDbContext _context;

    public ScholarshipModeratorController(ApplicationDbContext context)
    {
        _context = context;
    }

    // DASHBOARD
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingInstitutionsCount = await _context.InstitutionProfiles
            .CountAsync(i => i.VerificationStatus == "PendingModeratorReview");

        ViewBag.PendingListingsCount = await _context.Scholarships
            .CountAsync(s => s.Status == "Pending");

        return View();
    }

    // ============ INSTITUTION VETTING ============

    public async Task<IActionResult> Institutions()
    {
        var pending = await _context.InstitutionProfiles
            .Where(i => i.VerificationStatus == "PendingModeratorReview")
            .OrderBy(i => i.InstitutionName)
            .ToListAsync();

        return View(pending);
    }

    public async Task<IActionResult> InstitutionDetails(int id)
    {
        var institution = await _context.InstitutionProfiles.FindAsync(id);
        if (institution == null) return NotFound();

        var account = await _context.Users.FindAsync(institution.UserId);
        ViewBag.AccountEmail = account?.Email;

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

        await _context.SaveChangesAsync();
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

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"'{institution.InstitutionName}' has been rejected.";
        return RedirectToAction(nameof(Institutions));
    }

    // ============ SCHOLARSHIP LISTING MODERATION ============

    public async Task<IActionResult> Listings()
    {
        var pending = await _context.Scholarships
            .Where(s => s.Status == "Pending")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        return View(pending);
    }

    public async Task<IActionResult> ListingDetails(int id)
    {
        var scholarship = await _context.Scholarships.FindAsync(id);
        if (scholarship == null) return NotFound();

        var account = await _context.Users.FindAsync(scholarship.CreatedByUserId);
        ViewBag.AccountEmail = account?.Email;

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

        await _context.SaveChangesAsync();
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

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"'{scholarship.Title}' has been rejected.";
        return RedirectToAction(nameof(Listings));
    }
}
