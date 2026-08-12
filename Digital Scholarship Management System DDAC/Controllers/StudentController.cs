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

        // DASHBOARD & SCHOLARSHIP 
        public async Task<IActionResult> Index()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var profile = await _context.StudentProfiles
                .FirstOrDefaultAsync(p => p.UserId == currentUserId);

            if (profile == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            // Automated Scholarship Matching
            var matchedScholarships = await _context.Scholarships
                .Where(s => s.Status == "Open" &&
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
            if (scholarship == null || scholarship.Status != "Open") return NotFound();

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

        // 3. APPLY FOR SCHOLARSHIP & UPLOAD DOC (POST)
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

            if (model.DocumentFile != null && model.DocumentFile.Length > 0)
            {
                // Upload file locally to wwwroot/uploads
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.DocumentFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.DocumentFile.CopyToAsync(stream);
                }

                // 1. Create Application
                var application = new Application
                {
                    ScholarshipId = model.ScholarshipId,
                    StudentId = currentUserId,
                    Status = "Submitted",
                    SubmittedAt = DateTime.UtcNow
                };

                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                // 2. Create Document
                var document = new Document
                {
                    ApplicationId = application.ApplicationId,
                    DocumentType = model.DocumentType,
                    FileName = model.DocumentFile.FileName,
                    FilePath = "/uploads/" + uniqueFileName,
                    UploadedAt = DateTime.UtcNow,
                    VerificationStatus = "Pending"
                };

                _context.Documents.Add(document);

                // 3. Create Automated Notification
                var notification = new Notification
                {
                    UserId = currentUserId,
                    Message = $"Your application for '{scholarship.Title}' has been successfully submitted.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Application submitted successfully!";
                return RedirectToAction(nameof(TrackStatus));
            }

            ModelState.AddModelError("", "Please select a document to upload.");
            return View("Apply", model);
        }

        // 4. APPLICATION TRACKER
        public async Task<IActionResult> TrackStatus()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Query applications and join with Scholarship & Document models
            var trackingList = await (from app in _context.Applications
                                      where app.StudentId == currentUserId
                                      join sch in _context.Scholarships on app.ScholarshipId equals sch.ScholarshipId
                                      join doc in _context.Documents on app.ApplicationId equals doc.ApplicationId into docGroup
                                      from doc in docGroup.DefaultIfEmpty()
                                      orderby app.SubmittedAt descending
                                      select new ApplicationTrackerViewModel
                                      {
                                          ApplicationId = app.ApplicationId,
                                          ScholarshipTitle = sch.Title,
                                          Status = app.Status,
                                          SubmittedAt = app.SubmittedAt,
                                          DocumentType = doc != null ? doc.DocumentType : null,
                                          DocumentFileName = doc != null ? doc.FileName : null,
                                          DocumentPath = doc != null ? doc.FilePath : null,
                                          VerificationStatus = doc != null ? doc.VerificationStatus : "Pending"
                                      }).ToListAsync();

            return View(trackingList);
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
    }
}