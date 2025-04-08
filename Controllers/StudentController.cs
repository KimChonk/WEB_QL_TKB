using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TimetableManagementApp.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using TimetableManagementApp.ViewModels;

namespace TimetableManagementApp.Controllers
{
    [Authorize(Policy = "RequireStudentRole")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Trang chủ sinh viên
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserName = user?.FullName;
            
            return View();
        }

        // Xem danh sách khóa học đã đăng ký
        public async Task<IActionResult> MyCourses()
        {
            // Lấy thông tin người dùng hiện tại
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Lấy danh sách khóa học đã đăng ký của sinh viên
            var enrollments = await _context.CourseEnrollments
                .Where(ce => ce.StudentId == user.Id)
                .AsNoTracking()
                .ToListAsync();

            var courses = new List<CourseEnrollmentViewModel>();
            
            // Lấy thông tin chi tiết của từng khóa học
            foreach (var enrollment in enrollments)
            {
                var course = await _context.Courses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CourseCode == enrollment.CourseCode);
                
                if (course != null)
                {
                    // Lấy thông tin lịch học của khóa học
                    var timetables = await _context.Timetables
                        .Where(t => t.CourseCode == course.CourseCode)
                        .Include(t => t.Lecturer)
                        .Include(t => t.Room)
                        .Include(t => t.Class)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    courses.Add(new CourseEnrollmentViewModel
                    {
                        Course = course,
                        EnrollmentDate = enrollment.EnrollmentDate,
                        Timetables = timetables
                    });
                }
            }

            return View(courses);
        }

        // Xem chi tiết khóa học
        public async Task<IActionResult> CourseDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // Lấy thông tin người dùng hiện tại
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra xem sinh viên đã đăng ký khóa học này chưa
            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(ce => ce.CourseCode == id && ce.StudentId == user.Id);
                
            if (enrollment == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng ký khóa học này.";
                return RedirectToAction(nameof(MyCourses));
            }

            // Lấy thông tin chi tiết khóa học
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == id);
                
            if (course == null)
            {
                return NotFound();
            }

            // Lấy thông tin lịch học
            var timetables = await _context.Timetables
                .Where(t => t.CourseCode == id)
                .Include(t => t.Lecturer)
                .Include(t => t.Room)
                .Include(t => t.Class)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartPeriod)
                .ToListAsync();

            var viewModel = new CourseDetailsViewModel
            {
                Course = course,
                EnrollmentDate = enrollment.EnrollmentDate,
                Timetables = timetables
            };

            return View(viewModel);
        }

        // Xem thời khóa biểu đầy đủ theo tuần
        public async Task<IActionResult> Timetable(int? week = null)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Tính toán tuần hiện tại
                DateTime today = DateTime.Today;
                int currentWeekNumber = (week.HasValue) ? week.Value : GetWeekNumber(today);
                DateTime weekStart = FirstDateOfWeekISO8601(today.Year, currentWeekNumber);
                DateTime weekEnd = weekStart.AddDays(6);

                ViewBag.CurrentWeek = $"{weekStart:dd/MM/yyyy} - {weekEnd:dd/MM/yyyy}";
                ViewBag.CurrentWeekNumber = currentWeekNumber;

                // Lấy danh sách khóa học đã đăng ký bằng cách lặp qua từng bản ghi
                var enrollments = await _context.CourseEnrollments
                    .Where(ce => ce.StudentId == user.Id)
                    .AsNoTracking()
                    .ToListAsync();

                // Tạo danh sách mã khóa học để sử dụng trong truy vấn sau
                var enrolledCourseIds = enrollments.Select(e => e.CourseCode).ToList();

                // Lấy thời khóa biểu bằng cách truy vấn từng khóa học một và gom lại
                List<Timetable> allTimetables = new List<Timetable>();
                
                // Truy vấn từng khóa học một để tránh lỗi với OPENJSON
                foreach (var courseId in enrolledCourseIds)
                {
                    var timetablesForCourse = await _context.Timetables
                        .Where(t => t.CourseCode == courseId)
                        .Include(t => t.Course)
                        .Include(t => t.Room)
                        .Include(t => t.Lecturer)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    allTimetables.AddRange(timetablesForCourse);
                }

                return View(allTimetables.OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartPeriod).ToList());
            }
            catch (Exception ex)
            {
                // Xử lý lỗi và ghi nhật ký
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thời khóa biểu: " + ex.Message;
                
                // Trả về view model trống để tránh lỗi rendering
                return View(new List<Timetable>());
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

        // Action cho trang Thông báo
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Tạo view model trống cho trang thông báo
            var model = new List<object>(); // Có thể thay bằng model thông báo thích hợp

            // Hiển thị một thông báo tạm thời vì tính năng đang phát triển
            TempData["InfoMessage"] = "Tính năng thông báo đang được phát triển.";
            
            return View(model);
        }

        // Xem thông tin điểm danh cho một buổi học
        [HttpGet]
        public async Task<IActionResult> Attendance(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy thông tin buổi học từ id của lịch học
                var timetable = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Room)
                    .Include(t => t.Lecturer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TimetableID == id);

                if (timetable == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                    return RedirectToAction(nameof(Timetable));
                }

                // Lấy thông tin người dùng
                var userEmail = User.Identity.Name;
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Kiểm tra xem sinh viên có đăng ký học phần này không
                var enrollment = await _context.CourseEnrollments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ce => ce.CourseCode == timetable.CourseCode && ce.StudentId == user.Id);

                if (enrollment == null)
                {
                    TempData["ErrorMessage"] = "Bạn không được đăng ký vào khóa học này.";
                    return RedirectToAction(nameof(Timetable));
                }

                // Lấy thông tin ngày hiện tại và kiểm tra xem có phải ngày học không
                var today = DateTime.Now.Date;
                var dayOfWeek = (int)today.DayOfWeek + 1; // Convert từ DayOfWeek sang 1-7 (Thứ 2 - Chủ nhật)
                
                // Quy đổi dayOfWeek cho đúng định dạng (Chủ nhật = 7, Thứ 2 = 1, ..., Thứ 7 = 6)
                if (dayOfWeek == 1) dayOfWeek = 7; // Chủ nhật
                else dayOfWeek -= 1;

                var canAttend = timetable.DayOfWeek == dayOfWeek;

                // Kiểm tra xem sinh viên đã điểm danh chưa
                var attendanceToday = await _context.Attendances
                    .Where(a => a.TimetableID == id && a.StudentId == user.Id && a.AttendanceDate.Date == today)
                    .FirstOrDefaultAsync();

                var alreadyAttended = attendanceToday != null && attendanceToday.IsPresent;

                // Lấy lịch sử điểm danh
                var attendanceHistory = await _context.Attendances
                    .Where(a => a.TimetableID == id && a.StudentId == user.Id)
                    .OrderByDescending(a => a.AttendanceDate)
                    .AsNoTracking()
                    .ToListAsync();

                // Truyền dữ liệu sang view bằng StudentAttendanceViewModel
                var viewModel = new StudentAttendanceViewModel
                {
                    Timetable = timetable,
                    Course = timetable.Course,
                    Attendance = attendanceToday ?? new Attendance(),
                    CanAttend = canAttend,
                    IsAttended = alreadyAttended,
                    AttendanceTime = attendanceToday?.AttendanceTime,
                    AttendanceMethod = attendanceToday?.AttendanceMethod,
                    AttendanceHistory = attendanceHistory
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin điểm danh: " + ex.Message;
                return RedirectToAction(nameof(Timetable));
            }
        }

        // Xác nhận điểm danh
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAttendanceManual(int timetableId, string notes)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin buổi học
            var timetable = await _context.Timetables
                .FirstOrDefaultAsync(t => t.TimetableID == timetableId);

            if (timetable == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                return RedirectToAction(nameof(Timetable));
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Kiểm tra xem sinh viên có đăng ký học phần này không
            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(ce => ce.CourseCode == timetable.CourseCode && ce.StudentId == user.Id);

            if (enrollment == null)
            {
                TempData["ErrorMessage"] = "Bạn không được đăng ký vào khóa học này.";
                return RedirectToAction(nameof(Timetable));
            }

            // Kiểm tra xem có phải ngày học không
            var today = DateTime.Now.Date;
            var dayOfWeek = (int)today.DayOfWeek + 1;
            if (dayOfWeek == 1) dayOfWeek = 7;
            else dayOfWeek -= 1;

            if (timetable.DayOfWeek != dayOfWeek)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể điểm danh vào đúng ngày học.";
                return RedirectToAction(nameof(Attendance), new { id = timetableId });
            }

            // Kiểm tra xem sinh viên đã điểm danh chưa
            var attendanceToday = await _context.Attendances
                .FirstOrDefaultAsync(a => a.TimetableID == timetableId && a.StudentId == user.Id && a.AttendanceDate.Date == today);

            if (attendanceToday != null)
            {
                TempData["ErrorMessage"] = "Bạn đã điểm danh cho buổi học này rồi.";
                return RedirectToAction(nameof(Attendance), new { id = timetableId });
            }

            // Tạo bản ghi điểm danh mới
            var newAttendance = new Attendance
            {
                TimetableID = timetableId,
                StudentId = user.Id,
                AttendanceDate = DateTime.Now,
                IsPresent = true,
                Note = notes ?? string.Empty
            };

            _context.Attendances.Add(newAttendance);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Điểm danh thành công!";
            return RedirectToAction(nameof(Attendance), new { id = timetableId });
        }

        // Màn hình điểm danh bằng mã QR hoặc mã 4 số
        [HttpGet]
        public async Task<IActionResult> ConfirmAttendance(int timetableId, string code = null)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy thông tin buổi học
                var timetable = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Room)
                    .Include(t => t.Lecturer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TimetableID == timetableId);

                if (timetable == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin buổi học.";
                    return RedirectToAction(nameof(Timetable));
                }

                // Kiểm tra xem sinh viên có đăng ký học phần này không
                var enrollment = await _context.CourseEnrollments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ce => ce.CourseCode == timetable.CourseCode && ce.StudentId == user.Id);

                if (enrollment == null)
                {
                    TempData["ErrorMessage"] = "Bạn không được đăng ký vào khóa học này.";
                    return RedirectToAction(nameof(Timetable));
                }

                // Ngày hiện tại
                var today = DateTime.Now.Date;

                // Kiểm tra xem đã có bản ghi điểm danh chưa
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.TimetableID == timetableId && a.StudentId == user.Id && a.AttendanceDate.Date == today);

                bool alreadyAttended = attendance != null && attendance.IsPresent;
                bool canAttend = true; // Mặc định có thể điểm danh

                // Nếu có mã code từ QR, thử điểm danh luôn
                if (!string.IsNullOrEmpty(code) && attendance != null && !alreadyAttended)
                {
                    bool codeValid = attendance.AttendanceCode == code;
                    bool codeNotExpired = attendance.CodeGeneratedTime.HasValue && 
                        (DateTime.Now - attendance.CodeGeneratedTime.Value).TotalMinutes <= 15; // Mã có hiệu lực 15 phút

                    if (codeValid && codeNotExpired)
                    {
                        // Cập nhật điểm danh
                        attendance.IsPresent = true;
                        attendance.AttendanceTime = DateTime.Now;
                        attendance.Note = "Có mặt (QR)";
                        attendance.AttendanceMethod = "QR";
                        
                        await _context.SaveChangesAsync();
                        
                        TempData["SuccessMessage"] = "Điểm danh thành công!";
                        alreadyAttended = true;
                    }
                    else if (!codeValid)
                    {
                        TempData["ErrorMessage"] = "Mã điểm danh không hợp lệ.";
                    }
                    else if (!codeNotExpired)
                    {
                        TempData["ErrorMessage"] = "Mã điểm danh đã hết hạn.";
                    }
                }

                // Lấy lịch sử điểm danh
                var attendanceHistory = await _context.Attendances
                    .Where(a => a.TimetableID == timetableId && a.StudentId == user.Id)
                    .AsNoTracking()
                    .OrderByDescending(a => a.AttendanceDate)
                    .ToListAsync();

                // Nếu chưa có bản ghi điểm danh, có thể tạo mới
                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        TimetableID = timetableId,
                        StudentId = user.Id,
                        AttendanceDate = today,
                        IsPresent = false,
                        Note = "Chưa điểm danh"
                    };
                }

                // Tạo view model
                var viewModel = new StudentAttendanceViewModel
                {
                    Timetable = timetable,
                    Course = timetable.Course,
                    Attendance = attendance,
                    CanAttend = canAttend,
                    IsAttended = alreadyAttended,
                    AttendanceTime = attendance?.AttendanceTime,
                    AttendanceMethod = attendance?.AttendanceMethod,
                    AttendanceHistory = attendanceHistory
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                TempData["ErrorMessage"] = "Đã xảy ra lỗi: " + ex.Message;
                return RedirectToAction(nameof(Timetable));
            }
        }

        // Điểm danh bằng mã 4 số
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAttendanceCode(int timetableId, string attendanceCode)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Nếu không nhập mã
            if (string.IsNullOrEmpty(attendanceCode))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập mã điểm danh.";
                return RedirectToAction(nameof(ConfirmAttendance), new { timetableId });
            }

            try
            {
                // Ngày hiện tại
                var today = DateTime.Now.Date;

                // Kiểm tra xem đã có bản ghi điểm danh chưa
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.TimetableID == timetableId && a.StudentId == user.Id && a.AttendanceDate.Date == today);

                if (attendance == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin điểm danh.";
                    return RedirectToAction(nameof(ConfirmAttendance), new { timetableId });
                }

                // Nếu đã điểm danh rồi
                if (attendance.IsPresent)
                {
                    TempData["InfoMessage"] = "Bạn đã điểm danh thành công trước đó.";
                    return RedirectToAction(nameof(ConfirmAttendance), new { timetableId });
                }

                // Kiểm tra mã điểm danh
                bool codeValid = attendance.AttendanceCode == attendanceCode;
                bool codeNotExpired = attendance.CodeGeneratedTime.HasValue && 
                    (DateTime.Now - attendance.CodeGeneratedTime.Value).TotalMinutes <= 15; // Mã có hiệu lực 15 phút

                if (codeValid && codeNotExpired)
                {
                    // Cập nhật điểm danh
                    attendance.IsPresent = true;
                    attendance.AttendanceTime = DateTime.Now;
                    attendance.Note = "Có mặt (Code)";
                    attendance.AttendanceMethod = "Code";
                    
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Điểm danh thành công!";
                }
                else if (!codeValid)
                {
                    TempData["ErrorMessage"] = "Mã điểm danh không hợp lệ.";
                }
                else if (!codeNotExpired)
                {
                    TempData["ErrorMessage"] = "Mã điểm danh đã hết hạn.";
                }

                return RedirectToAction(nameof(ConfirmAttendance), new { timetableId });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi điểm danh: " + ex.Message;
                return RedirectToAction(nameof(ConfirmAttendance), new { timetableId });
            }
        }

        // Xem lịch sử điểm danh cho một khóa học
        public async Task<IActionResult> AttendanceHistory(string courseCode)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Kiểm tra đăng ký khóa học
                var enrollment = await _context.CourseEnrollments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ce => ce.CourseCode == courseCode && ce.StudentId == user.Id);

                if (enrollment == null)
                {
                    TempData["ErrorMessage"] = "Bạn không đăng ký vào khóa học này.";
                    return RedirectToAction(nameof(MyCourses));
                }

                // Lấy thông tin khóa học
                var course = await _context.Courses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CourseCode == courseCode);

                if (course == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin khóa học.";
                    return RedirectToAction(nameof(MyCourses));
                }

                // Lấy tất cả buổi học của khóa học
                var timetables = await _context.Timetables
                    .Where(t => t.CourseCode == courseCode)
                    .AsNoTracking()
                    .ToListAsync();

                // Lấy tất cả điểm danh của sinh viên cho khóa học này
                // Truy vấn an toàn theo từng bước để tránh lỗi SQL
                var attendances = new List<Attendance>();
                foreach (var timetable in timetables)
                {
                    var timetableAttendances = await _context.Attendances
                        .Where(a => a.TimetableID == timetable.TimetableID && a.StudentId == user.Id)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    attendances.AddRange(timetableAttendances);
                }

                // Thực hiện thêm phần preloading model cho các timetable tương ứng
                foreach (var attendance in attendances)
                {
                    // Tìm và gán timetable tương ứng
                    attendance.Timetable = timetables.FirstOrDefault(t => t.TimetableID == attendance.TimetableID);
                }

                // Tính tổng số buổi học và số buổi có mặt
                int totalSessions = timetables.Count;
                int presentSessions = attendances.Count(a => a.IsPresent);

                // Tạo view model
                var viewModel = new AttendanceHistoryViewModel
                {
                    Course = course,
                    Attendances = attendances,
                    Timetables = timetables,
                    TotalSessions = totalSessions,
                    PresentSessions = presentSessions
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải lịch sử điểm danh: " + ex.Message;
                return RedirectToAction(nameof(MyCourses));
            }
        }

        // Xem thời khóa biểu cá nhân
        public async Task<IActionResult> MyTimetable()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin người dùng
            var userEmail = User.Identity.Name;
            var user = await _userManager.FindByEmailAsync(userEmail);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy danh sách khóa học đã đăng ký
                var enrollments = await _context.CourseEnrollments
                    .Where(ce => ce.StudentId == user.Id)
                    .AsNoTracking()
                    .ToListAsync();

                var enrolledCourseIds = enrollments.Select(e => e.CourseCode).ToList();

                // Lấy thông tin tất cả khóa học
                var courses = new List<Course>();
                foreach (var courseId in enrolledCourseIds)
                {
                    var course = await _context.Courses
                        .Where(c => c.CourseCode == courseId)
                        .AsNoTracking()
                        .FirstOrDefaultAsync();
                    
                    if (course != null)
                    {
                        courses.Add(course);
                    }
                }

                // Lấy thời khóa biểu
                var timetables = new List<Timetable>();
                foreach (var courseId in enrolledCourseIds)
                {
                    var courseTimetables = await _context.Timetables
                        .Where(t => t.CourseCode == courseId)
                        .Include(t => t.Course)
                        .Include(t => t.Room)
                        .Include(t => t.Lecturer)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    timetables.AddRange(courseTimetables);
                }

                // Sắp xếp timetables theo ngày và tiết học
                timetables = timetables
                    .OrderBy(t => t.DayOfWeek)
                    .ThenBy(t => t.StartPeriod)
                    .ToList();

                var viewModel = new StudentTimetableViewModel
                {
                    Student = user,
                    Courses = courses,
                    Timetables = timetables
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Xử lý lỗi và ghi nhật ký
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thời khóa biểu: " + ex.Message;
                
                // Trả về view model trống để tránh lỗi rendering
                return View(new StudentTimetableViewModel
                {
                    Student = user,
                    Courses = new List<Course>(),
                    Timetables = new List<Timetable>()
                });
            }
        }
    }
} 