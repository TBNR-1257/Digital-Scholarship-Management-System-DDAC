using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Digital_Scholarship_Management_System_DDAC.Controllers;

[Authorize(Roles = "Moderator")]
public class ScholarshipModeratorController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
