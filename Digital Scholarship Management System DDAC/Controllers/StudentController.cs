using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Digital_Scholarship_Management_System_DDAC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IS3Service _s3Service;

    public StudentController(ApplicationDbContext context, IS3Service s3Service)
    {
        _context = context;
        _s3Service = s3Service;
    }

    // 1. DASHBOARD & AUTOMATED SCHOLARSHIP MATCHING
    public async Task<IActionResult> Index()
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == currentUserId);

        if (profile == null)
        {
            return RedirectToAction(nameof(Profile));
        }

        var matchedScholarships = await _context.Scholarships
            .Where(s => (s.Status == "Open" || s.Status == "Approved") &&
                        (s.ApplicationDeadline == null || s.ApplicationDeadline >= DateTime.UtcNow) &&
                        (s.MinCgpa == null || profile.CurrentCGPA >= s.MinCgpa) &&
                        (s.MaxHouseholdIncome == null || profile.HouseholdIncome == null || profile.HouseholdIncome <= s.MaxHouseholdIncome) &&
                        (string.IsNullOrEmpty(s.RequiredProgram) || s.RequiredProgram == "All" || s.RequiredProgram == profile.ProgramOfStudy))
            .ToListAsync();

        var notifications = await _context.Notifications
            .Where(n => n.UserId == currentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.StudentProfile = profile;
        ViewBag.Notifications = notifications;
        return View(matchedScholarships);
    }

    // 2. PROFILE MANAGEMENT (GET)
    public async Task<IActionResult> Profile()
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == currentUserId);

        if (profile == null)
        {
            return View(new StudentProfile { UserId = currentUserId });
        }

        return View(profile);
    }

    // 2. PROFILE MANAGEMENT (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(StudentProfile profile)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        profile.UserId = currentUserId;

        if (ModelState.IsValid)
        {
            if (profile.StudentProfileId == 0)
            {
                _context.StudentProfiles.Add(profile);
            }
            else
            {
                _context.StudentProfiles.Update(profile);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Profile saved successfully!";
            return RedirectToAction(nameof(Index));
        }

        return View("Profile", profile);
    }

    // 3. APPLY FOR SCHOLARSHIP (GET)
    public async Task<IActionResult> Apply(int scholarshipId)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var scholarship = await _context.Scholarships.FindAsync(scholarshipId);

        if (scholarship == null || (scholarship.Status != "Open" && scholarship.Status != "Approved"))
            return NotFound();

        bool hasAlreadyApplied = await _context.Applications
            .AnyAsync(a => a.ScholarshipId == scholarshipId && a.StudentId == currentUserId);

        if (hasAlreadyApplied)
        {
            TempData["ErrorMessage"] = $"You have already submitted an application for '{scholarship.Title}'.";
            return RedirectToAction(nameof(TrackStatus));
        }

        var model = new ApplicationSubmissionViewModel
        {
            ScholarshipId = scholarship.ScholarshipId,
            ScholarshipTitle = scholarship.Title
        };

        ViewBag.Scholarship = scholarship;

        if (!string.IsNullOrWhiteSpace(scholarship.PolicyFrameworkDocumentPath))
            ViewBag.PolicyFrameworkUrl = await _s3Service.GetViewUrlAsync(scholarship.PolicyFrameworkDocumentPath);
        if (!string.IsNullOrWhiteSpace(scholarship.EligibilityCriteriaDocumentPath))
            ViewBag.EligibilityCriteriaUrl = await _s3Service.GetViewUrlAsync(scholarship.EligibilityCriteriaDocumentPath);
        if (!string.IsNullOrWhiteSpace(scholarship.AllocationBudgetDocumentPath))
            ViewBag.AllocationBudgetUrl = await _s3Service.GetViewUrlAsync(scholarship.AllocationBudgetDocumentPath);
        if (!string.IsNullOrWhiteSpace(scholarship.PrivacyPolicyDocumentPath))
            ViewBag.PrivacyPolicyUrl = await _s3Service.GetViewUrlAsync(scholarship.PrivacyPolicyDocumentPath);

        return View(model);
    }

    // 3. APPLY FOR SCHOLARSHIP & UPLOAD ALL REQUIRED DOCUMENTS (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitApplication(ApplicationSubmissionViewModel model)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == currentUserId);

        if (profile == null) return RedirectToAction(nameof(Profile));

        var scholarship = await _context.Scholarships.FindAsync(model.ScholarshipId);
        if (scholarship == null) return NotFound();

        bool hasAlreadyApplied = await _context.Applications
            .AnyAsync(a => a.ScholarshipId == model.ScholarshipId && a.StudentId == currentUserId);

        if (hasAlreadyApplied)
        {
            TempData["ErrorMessage"] = $"You have already submitted an application for '{scholarship.Title}'.";
            return RedirectToAction(nameof(TrackStatus));
        }

        if (!ModelState.IsValid)
        {
            model.ScholarshipTitle = scholarship.Title;
            ViewBag.Scholarship = scholarship;

            // FIX: Re-generate URLs if form submission fails so the view doesn't crash
            if (!string.IsNullOrWhiteSpace(scholarship.PolicyFrameworkDocumentPath))
                ViewBag.PolicyFrameworkUrl = await _s3Service.GetViewUrlAsync(scholarship.PolicyFrameworkDocumentPath);
            if (!string.IsNullOrWhiteSpace(scholarship.EligibilityCriteriaDocumentPath))
                ViewBag.EligibilityCriteriaUrl = await _s3Service.GetViewUrlAsync(scholarship.EligibilityCriteriaDocumentPath);
            if (!string.IsNullOrWhiteSpace(scholarship.AllocationBudgetDocumentPath))
                ViewBag.AllocationBudgetUrl = await _s3Service.GetViewUrlAsync(scholarship.AllocationBudgetDocumentPath);
            if (!string.IsNullOrWhiteSpace(scholarship.PrivacyPolicyDocumentPath))
                ViewBag.PrivacyPolicyUrl = await _s3Service.GetViewUrlAsync(scholarship.PrivacyPolicyDocumentPath);

            return View("Apply", model);
        }

        var application = new Application
        {
            ScholarshipId = model.ScholarshipId,
            StudentId = currentUserId,
            Status = "Submitted",
            SubmittedAt = DateTime.UtcNow
        };

        _context.Applications.Add(application);
        await _context.SaveChangesAsync();

        await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.Transcript, model.TranscriptFile);
        await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.IncomeProof, model.IncomeProofFile);
        await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.Certificate, model.CertificateFile);
        await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.IdCard, model.IdCardFile);

        _context.Notifications.Add(new Notification
        {
            UserId = currentUserId,
            Message = $"Your application for '{scholarship.Title}' has been successfully submitted.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Application submitted successfully!";
        return RedirectToAction(nameof(TrackStatus));
    }

    // 4. APPLICATION TRACKER
    public async Task<IActionResult> TrackStatus()
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var applications = await (from app in _context.Applications
                                  where app.StudentId == currentUserId
                                  join sch in _context.Scholarships on app.ScholarshipId equals sch.ScholarshipId
                                  orderby app.SubmittedAt descending
                                  select new { app.ApplicationId, app.Status, app.SubmittedAt, ScholarshipTitle = sch.Title, ScholarshipStatus = sch.Status })
                                  .ToListAsync();

        var applicationIds = applications.Select(a => a.ApplicationId).ToList();
        var documents = await _context.Documents
            .Where(d => applicationIds.Contains(d.ApplicationId))
            .ToListAsync();

        var trackingList = new List<ApplicationTrackerViewModel>();

        foreach (var a in applications)
        {
            var docViewModels = new List<ApplicationDocumentViewModel>();

            foreach (var d in documents.Where(doc => doc.ApplicationId == a.ApplicationId))
            {
                docViewModels.Add(new ApplicationDocumentViewModel
                {
                    DocumentId = d.DocumentId,
                    DocumentType = d.DocumentType,
                    DocumentTypeLabel = DocumentTypeCatalog.GetLabel(d.DocumentType),
                    FileName = d.FileName,

                    FilePath = await _s3Service.GetViewUrlAsync(d.FilePath),
                    VerificationStatus = d.VerificationStatus
                });
            }

            trackingList.Add(new ApplicationTrackerViewModel
            {
                ApplicationId = a.ApplicationId,
                ScholarshipTitle = a.ScholarshipTitle,
                Status = a.Status,
                ScholarshipStatus = a.ScholarshipStatus,
                SubmittedAt = a.SubmittedAt,
                Documents = docViewModels
            });
        }

        return View(trackingList);
    }

    // 5. EDIT APPLICATION DOCUMENTS WHILE STILL PENDING (GET)
    public async Task<IActionResult> EditApplication(int applicationId)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == currentUserId);

        if (application == null) return NotFound();

        if (application.Status == "Approved" || application.Status == "Rejected")
        {
            TempData["ErrorMessage"] = "This application has already been decided and can no longer be edited.";
            return RedirectToAction(nameof(TrackStatus));
        }

        var scholarship = await _context.Scholarships.FindAsync(application.ScholarshipId);
        var documents = await _context.Documents
            .Where(d => d.ApplicationId == applicationId)
            .ToListAsync();

        var transcriptDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Transcript);
        var incomeProofDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IncomeProof);
        var certificateDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Certificate);
        var idCardDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IdCard);

        var model = new ApplicationDocumentEditViewModel
        {
            ApplicationId = applicationId,
            ScholarshipTitle = scholarship?.Title ?? "Scholarship",
            CurrentTranscriptFileName = transcriptDoc?.FileName,

            CurrentTranscriptFilePath = transcriptDoc != null ? await _s3Service.GetViewUrlAsync(transcriptDoc.FilePath) : null,
            CurrentIncomeProofFileName = incomeProofDoc?.FileName,
            CurrentIncomeProofFilePath = incomeProofDoc != null ? await _s3Service.GetViewUrlAsync(incomeProofDoc.FilePath) : null,
            CurrentCertificateFileName = certificateDoc?.FileName,
            CurrentCertificateFilePath = certificateDoc != null ? await _s3Service.GetViewUrlAsync(certificateDoc.FilePath) : null,
            CurrentIdCardFileName = idCardDoc?.FileName,
            CurrentIdCardFilePath = idCardDoc != null ? await _s3Service.GetViewUrlAsync(idCardDoc.FilePath) : null
        };

        return View(model);
    }

    // 5. EDIT APPLICATION DOCUMENTS WHILE STILL PENDING (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditApplication(ApplicationDocumentEditViewModel model)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.ApplicationId == model.ApplicationId && a.StudentId == currentUserId);

        if (application == null) return NotFound();

        if (application.Status == "Approved" || application.Status == "Rejected")
        {
            TempData["ErrorMessage"] = "This application has already been decided and can no longer be edited.";
            return RedirectToAction(nameof(TrackStatus));
        }

        var documents = await _context.Documents
            .Where(d => d.ApplicationId == application.ApplicationId)
            .ToListAsync();

        if (!ModelState.IsValid)
        {
            var scholarship = await _context.Scholarships.FindAsync(application.ScholarshipId);
            model.ScholarshipTitle = scholarship?.Title ?? "Scholarship";

            var transcriptDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Transcript);
            var incomeProofDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IncomeProof);
            var certificateDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Certificate);
            var idCardDoc = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IdCard);

            model.CurrentTranscriptFileName = transcriptDoc?.FileName;

            model.CurrentTranscriptFilePath = transcriptDoc != null ? await _s3Service.GetViewUrlAsync(transcriptDoc.FilePath) : null;
            model.CurrentIncomeProofFileName = incomeProofDoc?.FileName;
            model.CurrentIncomeProofFilePath = incomeProofDoc != null ? await _s3Service.GetViewUrlAsync(incomeProofDoc.FilePath) : null;
            model.CurrentCertificateFileName = certificateDoc?.FileName;
            model.CurrentCertificateFilePath = certificateDoc != null ? await _s3Service.GetViewUrlAsync(certificateDoc.FilePath) : null;
            model.CurrentIdCardFileName = idCardDoc?.FileName;
            model.CurrentIdCardFilePath = idCardDoc != null ? await _s3Service.GetViewUrlAsync(idCardDoc.FilePath) : null;

            return View(model);
        }

        await ReplaceDocumentIfProvidedAsync(documents, application.ApplicationId, DocumentTypeCatalog.Transcript, model.TranscriptFile);
        await ReplaceDocumentIfProvidedAsync(documents, application.ApplicationId, DocumentTypeCatalog.IncomeProof, model.IncomeProofFile);
        await ReplaceDocumentIfProvidedAsync(documents, application.ApplicationId, DocumentTypeCatalog.Certificate, model.CertificateFile);
        await ReplaceDocumentIfProvidedAsync(documents, application.ApplicationId, DocumentTypeCatalog.IdCard, model.IdCardFile);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Application updated successfully.";
        return RedirectToAction(nameof(TrackStatus));
    }

    // MARK NOTIFICATION AS READ
    [HttpPost]
    public async Task<IActionResult> MarkNotificationRead(int notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    // 6. WITHDRAW / DELETE APPLICATION (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawApplication(int applicationId)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == currentUserId);

        if (application == null)
        {
            TempData["ErrorMessage"] = "Application not found or access denied.";
            return RedirectToAction(nameof(TrackStatus));
        }

        var scholarship = await _context.Scholarships.FindAsync(application.ScholarshipId);
        string scholarshipTitle = scholarship?.Title ?? "Scholarship";

        var documents = await _context.Documents
            .Where(d => d.ApplicationId == applicationId)
            .ToListAsync();

        foreach (var doc in documents)
        {
            if (!string.IsNullOrEmpty(doc.FilePath))
            {
                await _s3Service.DeleteFileAsync(doc.FilePath);
            }
        }

        _context.Documents.RemoveRange(documents);
        _context.Applications.Remove(application);

        var notification = new Notification
        {
            UserId = currentUserId,
            Message = $"Your application for '{scholarshipTitle}' has been successfully withdrawn.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Your application for '{scholarshipTitle}' and all submitted documents have been withdrawn.";
        return RedirectToAction(nameof(TrackStatus));
    }

    // DELETE A SINGLE NOTIFICATION
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotification(int notificationId)
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == currentUserId);

        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // CLEAR ALL NOTIFICATIONS
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllNotifications()
    {
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var userNotifications = await _context.Notifications
            .Where(n => n.UserId == currentUserId)
            .ToListAsync();

        if (userNotifications.Any())
        {
            _context.Notifications.RemoveRange(userNotifications);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "All notifications cleared.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ---- HELPERS ----

    private async Task AddDocumentAsync(int applicationId, string documentType, IFormFile? file)
    {
        if (file == null || file.Length == 0) return;

        string? filePath = await _s3Service.UploadFileAsync(file, "student-documents");
        if (string.IsNullOrEmpty(filePath)) return;

        _context.Documents.Add(new Document
        {
            ApplicationId = applicationId,
            DocumentType = documentType,
            FileName = file.FileName,
            FilePath = filePath,
            UploadedAt = DateTime.UtcNow,
            VerificationStatus = "Pending"
        });
    }

    private async Task ReplaceDocumentIfProvidedAsync(List<Document> existingDocuments, int applicationId, string documentType, IFormFile? newFile)
    {
        if (newFile == null || newFile.Length == 0) return;

        string? newFilePath = await _s3Service.UploadFileAsync(newFile, "student-documents");
        if (string.IsNullOrEmpty(newFilePath)) return;

        var existing = existingDocuments.FirstOrDefault(d => d.DocumentType == documentType);

        if (existing != null)
        {
            if (!string.IsNullOrEmpty(existing.FilePath))
            {
                await _s3Service.DeleteFileAsync(existing.FilePath);
            }

            existing.FileName = newFile.FileName;
            existing.FilePath = newFilePath;
            existing.UploadedAt = DateTime.UtcNow;
            existing.VerificationStatus = "Pending";
        }
        else
        {
            _context.Documents.Add(new Document
            {
                ApplicationId = applicationId,
                DocumentType = documentType,
                FileName = newFile.FileName,
                FilePath = newFilePath,
                UploadedAt = DateTime.UtcNow,
                VerificationStatus = "Pending"
            });
        }
    }
}