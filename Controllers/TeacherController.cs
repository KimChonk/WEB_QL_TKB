using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TimetableManagementApp.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using TimetableManagementApp.Data;
using TimetableManagementApp.ViewModels;
using System;
using Microsoft.Extensions.Logging;

namespace TimetableManagementApp.Controllers
{
    [Authorize(Policy = "RequireTeacherRole")]
    public class TeacherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TeacherController> _logger;

        public TeacherController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<TeacherController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // Trang chủ của giáo viên
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserName = user?.FullName;
            
            return View();
        }

        // Hiển thị danh sách các lớp học mà giáo viên phụ trách
        public async Task<IActionResult> MyClasses()
        {
            var user = await _userManager.GetUserAsync(User);
            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Email == user.Email);

            if (lecturer == null)
            {
                // Nếu không tìm thấy thông tin giáo viên trong hệ thống
                return View("NoLecturerInfo");
            }

            // Lấy tất cả lịch dạy của giáo viên đó
            var timetables = await _context.Timetables
                .Where(t => t.LecturerId == lecturer.LecturerId)
                .Include(t => t.Class)
                .Include(t => t.Course)
                .Include(t => t.Room)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartPeriod)
                .ToListAsync();

            return View(timetables);
        }

        // Xem thông tin chi tiết một lớp học
        public async Task<IActionResult> ClassDetails(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.Class)
                .Include(t => t.Course)
                .Include(t => t.Room)
                .FirstOrDefaultAsync(t => t.TimetableID == id);

            if (timetable == null)
            {
                return NotFound();
            }

            return View(timetable);
        }

        // Xem thời khóa biểu của giảng viên
        public async Task<IActionResult> MyTimetable(int? week = null)
        {
            try
            {
                // Kiểm tra nếu người dùng chưa đăng nhập, chuyển hướng đến trang đăng nhập
                if (User.Identity?.IsAuthenticated != true)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Tính toán tuần hiện tại
                DateTime today = DateTime.Today;
                int currentWeekNumber = (week.HasValue) ? week.Value : GetWeekNumber(today);
                DateTime weekStart = FirstDateOfWeekISO8601(today.Year, currentWeekNumber);
                DateTime weekEnd = weekStart.AddDays(6);

                ViewBag.CurrentWeek = $"{weekStart:dd/MM/yyyy} - {weekEnd:dd/MM/yyyy}";
                ViewBag.CurrentWeekNumber = currentWeekNumber;

                // Lấy thông tin người dùng hiện tại
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = await _userManager.FindByEmailAsync(userEmail);

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Lấy các mã khóa học mà người dùng đang giảng dạy - bằng 2 cách
                var courseIds = new List<string>();
                
                // Cách 1: Tìm theo ID người dùng nếu được liên kết trực tiếp trong Timetable
                var timetablesByUserId = await _context.Timetables
                    .Where(t => t.ApplicationUserId == user.Id)
                    .Include(t => t.Course)
                    .Include(t => t.Room)
                    .Include(t => t.Lecturer)
                    .AsNoTracking()
                    .ToListAsync();
                
                foreach (var t in timetablesByUserId)
                {
                    if (!courseIds.Contains(t.CourseCode))
                    {
                        courseIds.Add(t.CourseCode);
                    }
                }
                
                // Cách 2: Tìm theo LecturerId nếu user là giảng viên (lecturer)
                var lecturer = await _context.Lecturers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Email == user.Email);
                
                if (lecturer != null)
                {
                    var timetablesByLecturerId = await _context.Timetables
                        .Where(t => t.LecturerId == lecturer.LecturerId)
                        .Include(t => t.Course)
                        .Include(t => t.Room)
                        .Include(t => t.Lecturer)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    foreach (var t in timetablesByLecturerId)
                    {
                        if (!courseIds.Contains(t.CourseCode))
                        {
                            courseIds.Add(t.CourseCode);
                        }
                        
                        // Thêm vào danh sách timetables nếu chưa có
                        if (!timetablesByUserId.Any(existing => existing.TimetableID == t.TimetableID))
                        {
                            timetablesByUserId.Add(t);
                        }
                    }
                }
                
                // Lấy thông tin chi tiết của các khóa học
                var courses = new List<Course>();
                foreach (var courseId in courseIds)
                {
                    var course = await _context.Courses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CourseCode == courseId);
                    
                    if (course != null)
                    {
                        courses.Add(course);
                    }
                }

                // Tạo dictionary để lưu trữ thời khóa biểu theo khóa học
                var courseTimetables = new Dictionary<string, List<Timetable>>();
                foreach (var courseId in courseIds)
                {
                    courseTimetables[courseId] = timetablesByUserId
                        .Where(t => t.CourseCode == courseId)
                        .ToList();
                }

                var viewModel = new TeacherTimetableViewModel
                {
                    Teacher = user,
                    CourseTimetables = courseTimetables,
                    Courses = courses,
                    AllTimetables = timetablesByUserId.OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartPeriod).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Xử lý lỗi và ghi nhật ký
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thời khóa biểu: " + ex.Message;
                
                // Trả về view model trống để tránh lỗi rendering
                return View(new TeacherTimetableViewModel
                {
                    Teacher = await _userManager.GetUserAsync(User),
                    CourseTimetables = new Dictionary<string, List<Timetable>>(),
                    Courses = new List<Course>(),
                    AllTimetables = new List<Timetable>()
                });
            }
        }

        // Helper method to get ISO 8601 week number
        private int GetWeekNumber(DateTime date)
        {
            var day = (int)System.Globalization.CultureInfo.CurrentCulture.Calendar.GetDayOfWeek(date);
            return System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                date.AddDays(4 - (day == 0 ? 7 : day)),
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
        
        // Helper method to get the first date of a week
        private DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var weekNum = weekOfYear;
            if (firstWeek <= 1)
            {
                weekNum -= 1;
            }

            var result = firstThursday.AddDays(7 * weekNum - 3);
            return result;
        }

        // Hiển thị danh sách sinh viên của một khóa học
        public async Task<IActionResult> CourseStudents(string courseCode)
        {
            if (string.IsNullOrEmpty(courseCode))
            {
                return NotFound();
            }

            // Lấy thông tin người dùng hiện tại
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Tìm thông tin giảng viên từ email
            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Email == user.Email);

            if (lecturer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin giảng viên liên kết với tài khoản này.";
                return RedirectToAction(nameof(MyTimetable));
            }

            // Kiểm tra xem giảng viên có dạy khóa học này không dựa trên LecturerId
            var isTeachingCourse = await _context.Timetables
                .AnyAsync(t => t.LecturerId == lecturer.LecturerId && t.CourseCode == courseCode);

            if (!isTeachingCourse)
            {
                TempData["ErrorMessage"] = "Bạn không phải là giảng viên của khóa học này.";
                return RedirectToAction(nameof(MyTimetable));
            }

            // Lấy thông tin khóa học
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == courseCode);

            if (course == null)
            {
                return NotFound();
            }

            // Tránh sử dụng OPENJSON bằng cách lấy thủ công và map từng sinh viên
            var enrollments = await _context.CourseEnrollments
                .Where(ce => ce.CourseCode == courseCode)
                .AsNoTracking()
                .ToListAsync();
            
            var studentList = new List<StudentViewModel>();
            
            // Lấy từng sinh viên một để tránh lỗi SQL phức tạp
            foreach (var enrollment in enrollments)
            {
                var student = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == enrollment.StudentId);

                if (student != null)
                {
                    studentList.Add(new StudentViewModel
                    {
                        Id = student.Id,
                        FullName = student.FullName ?? "Unknown",
                        Email = student.Email ?? "Unknown",
                        EnrollmentDate = enrollment.EnrollmentDate
                    });
                }
            }

            var viewModel = new CourseStudentsViewModel
            {
                Course = course,
                Students = studentList
            };

            return View(viewModel);
        }

        // Sinh mã điểm danh
        [HttpGet]
        public async Task<IActionResult> GenerateAttendanceCode(int timetableId)
        {
            try
            {
                // Kiểm tra nếu người dùng chưa đăng nhập, chuyển hướng đến trang đăng nhập
                if (User.Identity?.IsAuthenticated != true)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Lấy thông tin người dùng hiện tại
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = await _userManager.FindByEmailAsync(userEmail);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng.";
                    return RedirectToAction(nameof(Index));
                }

                // Lấy thông tin giảng viên
                var lecturer = await _context.Lecturers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Email == user.Email);

                if (lecturer == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin giảng viên liên kết với tài khoản này.";
                    return RedirectToAction(nameof(Index));
                }

                // Lấy thông tin buổi học
                var timetable = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Room)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TimetableID == timetableId);

                if (timetable == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                    return RedirectToAction(nameof(MyTimetable));
                }

                // Kiểm tra xem giảng viên có phụ trách buổi học này không
                if (timetable.LecturerId != lecturer.LecturerId)
                {
                    TempData["ErrorMessage"] = "Bạn không phải là giảng viên của buổi học này.";
                    return RedirectToAction(nameof(MyTimetable));
                }

                // Tạo mã ngẫu nhiên 4 chữ số
                Random random = new Random();
                string attendanceCode = random.Next(1000, 10000).ToString();

                // Lấy danh sách sinh viên đã đăng ký khóa học
                // Sử dụng cách truy vấn an toàn từng bước để tránh lỗi SQL
                var enrolledStudents = await _context.CourseEnrollments
                    .Where(ce => ce.CourseCode == timetable.CourseCode)
                    .Include(ce => ce.Student)
                    .AsNoTracking()
                    .ToListAsync();

                // Ngày hiện tại
                var today = DateTime.Now.Date;

                // Kiểm tra xem đã có bản ghi điểm danh cho ngày hôm nay chưa
                var existingAttendances = await _context.Attendances
                    .Where(a => a.TimetableID == timetableId && a.AttendanceDate.Date == today)
                    .ToListAsync();

                // Nếu chưa có, tạo mới cho tất cả sinh viên đã đăng ký
                if (!existingAttendances.Any())
                {
                    List<Attendance> attendances = new List<Attendance>();
                    
                    foreach (var enrollment in enrolledStudents)
                    {
                        attendances.Add(new Attendance
                        {
                            TimetableID = timetableId,
                            StudentId = enrollment.StudentId,
                            AttendanceDate = today,
                            IsPresent = false,
                            Note = "Chưa điểm danh",
                            AttendanceCode = attendanceCode,
                            CodeGeneratedTime = DateTime.Now
                        });
                    }
                    
                    _context.Attendances.AddRange(attendances);
                    await _context.SaveChangesAsync();
                    
                    existingAttendances = attendances;
                }
                else
                {
                    // Cập nhật mã điểm danh mới và thời gian tạo mã
                    foreach (var attendance in existingAttendances)
                    {
                        attendance.AttendanceCode = attendanceCode;
                        attendance.CodeGeneratedTime = DateTime.Now;
                    }
                    
                    await _context.SaveChangesAsync();
                }

                // Trả về thông tin buổi học và mã điểm danh
                var viewModel = new AttendanceViewModel
                {
                    Timetable = timetable,
                    AttendanceCode = attendanceCode,
                    QRCodeUrl = $"/Teacher/AttendanceQR?timetableId={timetableId}&code={attendanceCode}",
                    CodeGeneratedTime = DateTime.Now,
                    EnrolledStudents = enrolledStudents.Select(e => e.Student).ToList(),
                    Attendances = existingAttendances
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tạo mã điểm danh: " + ex.Message;
                return RedirectToAction(nameof(MyTimetable));
            }
        }

        // Hiển thị mã QR
        [HttpGet]
        public async Task<IActionResult> AttendanceQR(int timetableId, string code)
        {
            // URL mà sinh viên sẽ dùng để điểm danh
            string attendanceUrl = $"{Request.Scheme}://{Request.Host}/Student/ConfirmAttendance?timetableId={timetableId}&code={code}";
            
            // Lưu URL vào ViewBag để truyền cho view
            ViewBag.AttendanceUrl = attendanceUrl;
            ViewBag.AttendanceCode = code;
            ViewBag.TimetableId = timetableId;

            // Lấy thông tin buổi học
            var timetable = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Room)
                .FirstOrDefaultAsync(t => t.TimetableID == timetableId);

            if (timetable == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                return RedirectToAction(nameof(Index));
            }

            return View(timetable);
        }

        // Xem và quản lý điểm danh của sinh viên
        [HttpGet]
        public async Task<IActionResult> ManageAttendance(int timetableId, DateTime? date = null)
        {
            // Lấy thông tin người dùng
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra thông tin buổi học
            var timetable = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Room)
                .FirstOrDefaultAsync(t => t.TimetableID == timetableId);

            if (timetable == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                return RedirectToAction(nameof(Index));
            }

            // Lấy thông tin giảng viên từ email
            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Email == user.Email);
            if (lecturer == null && !User.IsInRole("Administrator"))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin giảng viên cho tài khoản của bạn.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra quyền - chỉ giảng viên dạy môn này mới có quyền quản lý điểm danh
            if (lecturer != null && timetable.LecturerId != lecturer.LecturerId && !User.IsInRole("Administrator"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền quản lý điểm danh cho buổi học này.";
                return RedirectToAction(nameof(Index));
            }

            // Nếu không chọn ngày, mặc định là ngày hiện tại
            DateTime attendanceDate = date ?? DateTime.Now.Date;

            // Lấy danh sách điểm danh
            var attendances = await _context.Attendances
                .Where(a => a.TimetableID == timetableId && a.AttendanceDate.Date == attendanceDate.Date)
                .Include(a => a.Student)
                .ToListAsync();

            // Lấy danh sách sinh viên đã đăng ký học phần
            var enrolledStudents = await _context.CourseEnrollments
                .Where(ce => ce.CourseCode == timetable.CourseCode)
                .Include(ce => ce.Student)
                .ToListAsync();

            // Tạo view model
            var viewModel = new AttendanceManagementViewModel
            {
                Timetable = timetable,
                AttendanceDate = attendanceDate,
                Attendances = attendances,
                EnrolledStudents = enrolledStudents.Select(e => e.Student).ToList()
            };

            return View(viewModel);
        }

        // Cập nhật trạng thái điểm danh
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int attendanceId, bool isPresent, string note)
        {
            // Lấy thông tin người dùng
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra thông tin điểm danh
            var attendance = await _context.Attendances
                .Include(a => a.Timetable)
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);

            if (attendance == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin điểm danh." });
            }

            // Lấy thông tin giảng viên từ email
            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Email == user.Email);
            if (lecturer == null && !User.IsInRole("Administrator"))
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin giảng viên cho tài khoản của bạn." });
            }

            // Kiểm tra quyền - chỉ giảng viên dạy môn này mới có quyền cập nhật điểm danh
            if (lecturer != null && attendance.Timetable.LecturerId != lecturer.LecturerId && !User.IsInRole("Administrator"))
            {
                return Json(new { success = false, message = "Bạn không có quyền cập nhật điểm danh cho buổi học này." });
            }

            // Cập nhật trạng thái điểm danh
            attendance.IsPresent = isPresent;
            attendance.Note = note ?? (isPresent ? "Có mặt" : "Vắng mặt");
            attendance.AttendanceMethod = "Manual"; // Cập nhật bởi giảng viên
            
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật trạng thái điểm danh thành công." });
        }

        // Thêm API endpoint để lấy timetable theo khóa học
        [HttpGet]
        public async Task<IActionResult> GetTimetablesByCourse(string courseCode)
        {
            if (string.IsNullOrEmpty(courseCode))
            {
                return BadRequest("Mã khóa học không được để trống");
            }

            try
            {
                // Lấy thông tin người dùng hiện tại
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Lấy các bản ghi Timetable bằng 2 cách
                var timetablesByUserId = await _context.Timetables
                    .AsNoTracking()
                    .Where(t => t.ApplicationUserId == user.Id && t.CourseCode == courseCode)
                    .ToListAsync();
                
                // Tìm theo LecturerId nếu user là giảng viên
                var lecturer = await _context.Lecturers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Email == user.Email);
                
                var timetablesResult = timetablesByUserId;
                
                if (lecturer != null)
                {
                    var timetablesByLecturerId = await _context.Timetables
                        .AsNoTracking()
                        .Where(t => t.LecturerId == lecturer.LecturerId && t.CourseCode == courseCode)
                        .ToListAsync();
                    
                    // Kết hợp danh sách và loại bỏ trùng lặp
                    foreach (var timetable in timetablesByLecturerId)
                    {
                        if (!timetablesResult.Any(t => t.TimetableID == timetable.TimetableID))
                        {
                            timetablesResult.Add(timetable);
                        }
                    }
                }

                // Ánh xạ dữ liệu sang DTO để tránh vấn đề tham chiếu vòng lặp
                var result = timetablesResult.Select(t => new
                {
                    t.TimetableID,
                    t.CourseCode,
                    t.RoomID,
                    t.DayOfWeek,
                    t.StartPeriod,
                    t.NumPeriods,
                    t.DateStart,
                    t.DateEnd,
                    RoomName = t.Room != null ? t.Room.RoomName : t.RoomID,
                    CourseName = t.Course != null ? t.Course.CourseName : string.Empty
                }).OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartPeriod).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy dữ liệu timetable theo khóa học");
                return StatusCode(500, "Đã xảy ra lỗi khi xử lý yêu cầu");
            }
        }
    }
}