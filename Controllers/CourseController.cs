using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TimetableManagementApp.Controllers
{
    [Authorize(Roles = "Admin,Teacher")]
    public class CourseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CourseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Danh sách tất cả khóa học
        public async Task<IActionResult> Index()
        {
            var courses = await _context.Courses.ToListAsync();
            return View(courses);
        }

        // Chi tiết khóa học
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == id);

            if (course == null)
            {
                return NotFound();
            }

            // Lấy danh sách sinh viên đã đăng ký khóa học này
            var enrolledStudents = await _context.CourseEnrollments
                .Where(ce => ce.CourseCode == id)
                .Include(ce => ce.Student)
                .Select(ce => new EnrolledStudentViewModel
                {
                    Id = ce.StudentId,
                    FullName = ce.Student.FullName,
                    Email = ce.Student.Email,
                    EnrollmentDate = ce.EnrollmentDate
                })
                .ToListAsync();

            // Lấy thời khóa biểu của khóa học
            var timetables = await _context.Timetables
                .Where(t => t.CourseCode == id)
                .Include(t => t.Room)
                .Include(t => t.Lecturer)
                .ToListAsync();

            var viewModel = new CourseDetailsViewModel
            {
                Course = course,
                Timetables = timetables,
                StudentCount = enrolledStudents.Count,
                EnrolledStudents = enrolledStudents
            };

            return View(viewModel);
        }

        // Thêm sinh viên vào khóa học - GET
        public async Task<IActionResult> AddStudent(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == id);

            if (course == null)
            {
                return NotFound();
            }

            // Lấy danh sách sinh viên chưa đăng ký khóa học này
            var enrolledStudentIds = await _context.CourseEnrollments
                .Where(ce => ce.CourseCode == id)
                .Select(ce => ce.StudentId)
                .ToListAsync();

            var students = await _userManager.GetUsersInRoleAsync("Student");
            var availableStudents = students.Where(s => !enrolledStudentIds.Contains(s.Id)).ToList();

            var viewModel = new AddStudentToCourseViewModel
            {
                CourseCode = course.CourseCode,
                CourseName = course.CourseName,
                Students = availableStudents.Select(s => new SelectListItem
                {
                    Value = s.Id,
                    Text = $"{s.FullName} ({s.Email})"
                }).ToList()
            };

            return View(viewModel);
        }

        // Thêm sinh viên vào khóa học - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(AddStudentToCourseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var enrollment = await _context.CourseEnrollments
                    .FirstOrDefaultAsync(ce => ce.CourseCode == model.CourseCode && ce.StudentId == model.SelectedStudentId);

                if (enrollment == null)
                {
                    var newEnrollment = new CourseEnrollment
                    {
                        CourseCode = model.CourseCode,
                        StudentId = model.SelectedStudentId,
                        EnrollmentDate = DateTime.Now
                    };

                    _context.CourseEnrollments.Add(newEnrollment);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Sinh viên đã được thêm vào khóa học thành công.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Sinh viên đã đăng ký khóa học này.";
                }

                return RedirectToAction(nameof(Details), new { id = model.CourseCode });
            }

            return View(model);
        }

        // Xóa sinh viên khỏi khóa học
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(string studentId, string courseCode)
        {
            if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(courseCode))
            {
                return NotFound();
            }

            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(ce => ce.CourseCode == courseCode && ce.StudentId == studentId);

            if (enrollment != null)
            {
                _context.CourseEnrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Sinh viên đã được xóa khỏi khóa học thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đăng ký khóa học của sinh viên này.";
            }

            return RedirectToAction(nameof(Details), new { id = courseCode });
        }
    }
} 