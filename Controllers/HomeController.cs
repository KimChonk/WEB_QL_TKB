using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TimetableManagementApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace TimetableManagementApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _logger = logger;
        _userManager = userManager;
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult ImportGuide()
    {
        return View();
    }

    public IActionResult ImportFAQ()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    public async Task<IActionResult> RegisterCoursesForStudent0()
    {
        var student = await _userManager.FindByEmailAsync("student0@university.com");
        if (student == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản student0@university.com";
            return RedirectToAction(nameof(Index));
        }

        var courses = await _context.Courses.Take(3).ToListAsync();
        int count = 0;

        foreach (var course in courses)
        {
            var existingEnrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(ce => ce.CourseCode == course.CourseCode && ce.StudentId == student.Id);

            if (existingEnrollment == null)
            {
                var enrollment = new CourseEnrollment
                {
                    StudentId = student.Id,
                    CourseCode = course.CourseCode,
                    EnrollmentDate = DateTime.Now
                };

                _context.CourseEnrollments.Add(enrollment);
                count++;
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã đăng ký {count} khóa học cho tài khoản student0@university.com";
        return RedirectToAction(nameof(Index));
    }
}
