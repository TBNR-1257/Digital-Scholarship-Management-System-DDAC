using System.Security.Claims;
using Digital_Scholarship_Management_System_DDAC.Data;
using Digital_Scholarship_Management_System_DDAC.Models;
using Digital_Scholarship_Management_System_DDAC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Digital_Scholarship_Management_System_DDAC.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public StudentController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

            // Automated Matching Engine: Filters for APPROVED/OPEN listings and active deadlines
            var matchedScholarships = await _context.Scholarships
                .Where(s => (s.Status == "Open" || s.Status == "Approved") &&
                            (s.ApplicationDeadline == null || s.ApplicationDeadline >= DateTime.UtcNow) &&
                            (s.MinCgpa == null || profile.CurrentCGPA >= s.MinCgpa) &&
                            (s.MaxHouseholdIncome == null || profile.HouseholdIncome == null || profile.HouseholdIncome <= s.MaxHouseholdIncome) &&
                            (string.IsNullOrEmpty(s.RequiredProgram) || s.RequiredProgram == "All" || s.RequiredProgram == profile.ProgramOfStudy))
                .ToListAsync();

            // Fetch User Notifications
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

            // Prevent direct URL access if already applied
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

            // DUPLICATE CHECK
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

            await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.Transcript, model.TranscriptFile!);
            await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.IncomeProof, model.IncomeProofFile!);
            await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.Certificate, model.CertificateFile!);
            await AddDocumentAsync(application.ApplicationId, DocumentTypeCatalog.IdCard, model.IdCardFile!);

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
                                       select new { app.ApplicationId, app.Status, app.SubmittedAt, ScholarshipTitle = sch.Title })
                                      .ToListAsync();

            var applicationIds = applications.Select(a => a.ApplicationId).ToList();
            var documents = await _context.Documents
                .Where(d => applicationIds.Contains(d.ApplicationId))
                .ToListAsync();

            var trackingList = applications.Select(a => new ApplicationTrackerViewModel
            {
                ApplicationId = a.ApplicationId,
                ScholarshipTitle = a.ScholarshipTitle,
                Status = a.Status,
                SubmittedAt = a.SubmittedAt,
                Documents = documents
                    .Where(d => d.ApplicationId == a.ApplicationId)
                    .Select(ToDocumentViewModel)
                    .ToList()
            }).ToList();

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

            var model = new ApplicationDocumentEditViewModel
            {
                ApplicationId = applicationId,
                ScholarshipTitle = scholarship?.Title ?? "Scholarship",
                CurrentTranscriptFileName = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Transcript)?.FileName,
                CurrentTranscriptFilePath = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Transcript)?.FilePath,
                CurrentIncomeProofFileName = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IncomeProof)?.FileName,
                CurrentIncomeProofFilePath = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IncomeProof)?.FilePath,
                CurrentCertificateFileName = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Certificate)?.FileName,
                CurrentCertificateFilePath = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.Certificate)?.FilePath,
                CurrentIdCardFileName = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IdCard)?.FileName,
                CurrentIdCardFilePath = documents.FirstOrDefault(d => d.DocumentType == DocumentTypeCatalog.IdCard)?.FilePath
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

            // 1. Fetch application & verify ownership (security check)
            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == currentUserId);

            if (application == null)
            {
                TempData["ErrorMessage"] = "Application not found or access denied.";
                return RedirectToAction(nameof(TrackStatus));
            }

            // Optional: Fetch scholarship details for notification text
            var scholarship = await _context.Scholarships.FindAsync(application.ScholarshipId);
            string scholarshipTitle = scholarship?.Title ?? "Scholarship";

            // 2. Fetch associated documents
            var documents = await _context.Documents
                .Where(d => d.ApplicationId == applicationId)
                .ToListAsync();

            // 3. Delete physical files from wwwroot/uploads
            foreach (var doc in documents)
            {
                DeletePhysicalFile(doc.FilePath);
            }

            // 4. Remove database records
            _context.Documents.RemoveRange(documents);
            _context.Applications.Remove(application);

            // 5. Create automated withdrawal notification
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

        // ---- helpers ----

        private async Task AddDocumentAsync(int applicationId, string documentType, IFormFile file)
        {
            string filePath = await SaveUploadedFileAsync(file);

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

            string filePath = await SaveUploadedFileAsync(newFile);
            var existing = existingDocuments.FirstOrDefault(d => d.DocumentType == documentType);

            if (existing != null)
            {
                DeletePhysicalFile(existing.FilePath);
                existing.FileName = newFile.FileName;
                existing.FilePath = filePath;
                existing.UploadedAt = DateTime.UtcNow;
                existing.VerificationStatus = "Pending"; // replaced file needs re-verification
            }
            else
            {
                _context.Documents.Add(new Document
                {
                    ApplicationId = applicationId,
                    DocumentType = documentType,
                    FileName = newFile.FileName,
                    FilePath = filePath,
                    UploadedAt = DateTime.UtcNow,
                    VerificationStatus = "Pending"
                });
            }
        }

        private async Task<string> SaveUploadedFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/uploads/" + uniqueFileName;
        }

        private void DeletePhysicalFile(string? relativeFilePath)
        {
            if (string.IsNullOrEmpty(relativeFilePath)) return;

            string relativePath = relativeFilePath.TrimStart('/', '\\');
            string physicalPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }

        private static ApplicationDocumentViewModel ToDocumentViewModel(Document d) => new()
        {
            DocumentId = d.DocumentId,
            DocumentType = d.DocumentType,
            DocumentTypeLabel = DocumentTypeCatalog.GetLabel(d.DocumentType),
            FileName = d.FileName,
            FilePath = d.FilePath,
            VerificationStatus = d.VerificationStatus
        };
    }
}
