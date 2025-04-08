using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using TimetableManagementApp.Services;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TimetableManagementApp.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserImportService _userImportService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            UserImportService userImportService,
            ApplicationDbContext context,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _userImportService = userImportService;
            _context = context;
            _logger = logger;
        }

        // GET: Admin/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .ToListAsync();
            
            return View(users);
        }

        // GET: Admin/StudentUsers
        public async Task<IActionResult> StudentUsers()
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var studentUsers = students.OrderBy(u => u.FullName).ToList();
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_StudentUsersList", studentUsers);
            }
            
            return View("UsersByRole", studentUsers);
        }

        // GET: Admin/TeacherUsers
        public async Task<IActionResult> TeacherUsers()
        {
            var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
            var teacherUsers = teachers.OrderBy(u => u.FullName).ToList();
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_TeacherUsersList", teacherUsers);
            }
            
            ViewBag.UserType = "Giảng viên";
            return View("UsersByRole", teacherUsers);
        }

        // GET: Admin/AdminUsers
        public async Task<IActionResult> AdminUsers()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            var adminUsers = admins.OrderBy(u => u.FullName).ToList();
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_AdminUsersList", adminUsers);
            }
            
            ViewBag.UserType = "Quản trị viên";
            return View("UsersByRole", adminUsers);
        }

        // GET: Admin/ImportUsers
        public IActionResult ImportUsers()
        {
            var model = new UserImportViewModel();
            return View(model);
        }

        // POST: Admin/ImportUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportUsers(UserImportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
            {
                ModelState.AddModelError("ExcelFile", "Vui lòng chọn file Excel");
                return View(model);
            }

            if (!model.ExcelFile.FileName.EndsWith(".xlsx"))
            {
                ModelState.AddModelError("ExcelFile", "Vui lòng tải lên file Excel (.xlsx)");
                return View(model);
            }

            // Kiểm tra mật khẩu mặc định
            if (string.IsNullOrEmpty(model.DefaultPassword) || model.DefaultPassword.Length < 8)
            {
                ModelState.AddModelError("DefaultPassword", "Mật khẩu phải có ít nhất 8 ký tự");
                return View(model);
            }

            // Đảm bảo mật khẩu đủ mạnh
            if (!model.DefaultPassword.Any(char.IsUpper) || 
                !model.DefaultPassword.Any(char.IsLower) || 
                !model.DefaultPassword.Any(char.IsDigit) ||
                !model.DefaultPassword.Any(c => !char.IsLetterOrDigit(c)))
            {
                ModelState.AddModelError("DefaultPassword", "Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt");
                return View(model);
            }

            using (var stream = model.ExcelFile.OpenReadStream())
            {
                var result = await _userImportService.ImportUsersFromExcel(stream, model.UserType, model.DefaultPassword);
                
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                    // Lưu mật khẩu mặc định để hiển thị cho admin
                    TempData["DefaultPassword"] = model.DefaultPassword;
                    
                    // Lưu thông tin tài khoản vừa nhập
                    if (result.ImportedUsers != null && result.ImportedUsers.Count > 0)
                    {
                        TempData["ImportedUsers"] = JsonSerializer.Serialize(result.ImportedUsers);
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                // Lưu danh sách lỗi chi tiết nếu có
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    TempData["DetailedErrors"] = JsonSerializer.Serialize(result.Errors);
                }
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/UserDetails/id
        public async Task<IActionResult> UserDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Lấy danh sách các role của user
            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;

            return View(user);
        }

        // GET: Admin/DeleteUser/id
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Admin/DeleteUser/id
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Không cho phép xóa tài khoản admin
            if (await _userManager.IsInRoleAsync(user, "Administrator"))
            {
                TempData["ErrorMessage"] = "Không thể xóa tài khoản Admin";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Xóa tài khoản thành công";
            }
            else
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa tài khoản: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/ResetPassword/id
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ResetPasswordViewModel
            {
                UserId = id,
                UserEmail = user.Email,
                UserFullName = user.FullName
            };

            return View(model);
        }

        // POST: Admin/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng";
                return RedirectToAction(nameof(Users));
            }

            // Tạo token reset password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Reset password
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công";
                return RedirectToAction(nameof(UserDetails), new { id = model.UserId });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }
        }

        // GET: Admin/Courses
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses
                .OrderBy(c => c.CourseCode)
                .ToListAsync();
            
            return View(courses);
        }

        // GET: Admin/CourseDetails/id
        public async Task<IActionResult> CourseDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.CourseEnrollments)
                    .ThenInclude(ce => ce.Student)
                .FirstOrDefaultAsync(c => c.CourseCode == id);

            if (course == null)
            {
                return NotFound();
            }

            // Count timetables and lecturers for this course
            var timetables = await _context.Timetables
                .Where(t => t.CourseCode == id)
                .Include(t => t.Lecturer)
                .ToListAsync();

            // Get unique lecturers
            var lecturers = timetables
                .Select(t => t.Lecturer)
                .Where(l => l != null)
                .DistinctBy(l => l.LecturerId)
                .ToList();

            ViewBag.TimetableCount = timetables.Count;
            ViewBag.Lecturers = lecturers;
            ViewBag.StudentCount = course.CourseEnrollments.Count;

            return View(course);
        }

        // GET: Admin/EditCourse/id
        public async Task<IActionResult> EditCourse(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            return View(course);
        }

        // POST: Admin/EditCourse/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(string id, [Bind("CourseCode,CourseName,Credits,Department,Description")] Course course)
        {
            if (id != course.CourseCode)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật khóa học thành công";
                    return RedirectToAction(nameof(CourseDetails), new { id = course.CourseCode });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CourseExists(course.CourseCode))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(course);
        }

        // GET: Admin/Rooms
        public async Task<IActionResult> Rooms()
        {
            var rooms = await _context.Rooms
                .OrderBy(r => r.RoomID)
                .ToListAsync();
            
            return View(rooms);
        }

        // GET: Admin/RoomDetails/id
        public async Task<IActionResult> RoomDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .FirstOrDefaultAsync(r => r.RoomID == id);

            if (room == null)
            {
                return NotFound();
            }

            // Get timetables for this room
            var timetables = await _context.Timetables
                .Where(t => t.RoomID == id)
                .Include(t => t.Course)
                .Include(t => t.Lecturer)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartPeriod)
                .ToListAsync();

            ViewBag.Timetables = timetables;
            ViewBag.TimetableCount = timetables.Count;

            return View(room);
        }

        // GET: Admin/EditRoom/id
        public async Task<IActionResult> EditRoom(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // POST: Admin/EditRoom/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(string id, [Bind("RoomID,RoomName,Capacity,Location")] Room room)
        {
            if (id != room.RoomID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật phòng học thành công";
                    return RedirectToAction(nameof(RoomDetails), new { id = room.RoomID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.RoomID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(room);
        }

        // GET: Admin/ManageAccounts
        public IActionResult ManageAccounts()
        {
            return View();
        }

        // GET: Admin/AssignTeacher
        public async Task<IActionResult> AssignTeacher(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == id);

            if (course == null)
            {
                return NotFound();
            }

            // Get a list of teacher users
            var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
            
            // Get existing lecturers for this course from timetables
            var existingLecturerIds = await _context.Timetables
                .Where(t => t.CourseCode == id)
                .Select(t => t.LecturerId)
                .Distinct()
                .ToListAsync();

            ViewBag.Teachers = teachers.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})",
                Selected = existingLecturerIds.Contains(t.Id)
            }).ToList();

            ViewBag.Course = course;
            
            return View();
        }

        // POST: Admin/AssignTeacher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeacher(string courseCode, string teacherId, DateTime startDate, DateTime endDate, int dayOfWeek, int startPeriod, int numPeriods, string roomId)
        {
            try
            {
                if (string.IsNullOrEmpty(courseCode) || string.IsNullOrEmpty(teacherId))
                {
                    TempData["ErrorMessage"] = "Mã khóa học và ID giảng viên không được để trống";
                    return RedirectToAction(nameof(CourseDetails), new { id = courseCode });
                }

                var course = await _context.Courses.FindAsync(courseCode);
                if (course == null)
                {
                    return NotFound();
                }

                var teacher = await _userManager.FindByIdAsync(teacherId);
                if (teacher == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy giảng viên";
                    return RedirectToAction(nameof(CourseDetails), new { id = courseCode });
                }

                // Check if the room exists
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    // Create a new room if it doesn't exist
                    room = new Room
                    {
                        RoomID = roomId.Length > 20 ? roomId.Substring(0, 20) : roomId, // Đảm bảo RoomID không quá dài
                        RoomName = $"Phòng {roomId}",
                        Capacity = 40,
                        Location = "Chưa cập nhật"
                    };
                    _context.Rooms.Add(room);
                    await _context.SaveChangesAsync();
                }

                // *** Kiểm tra độ dài của teacherId ***
                // Nếu teacherId dài hơn 10 ký tự (giới hạn trong DB), cần tìm hoặc tạo bản ghi Lecturer
                string lecturerId = teacherId;
                if (teacherId.Length > 10)
                {
                    // Tìm hoặc tạo bản ghi Lecturer cho giảng viên này
                    var existingLecturer = await _context.Lecturers
                        .FirstOrDefaultAsync(l => l.Email == teacher.Email);

                    if (existingLecturer != null)
                    {
                        // Sử dụng LecturerID của bản ghi đã tồn tại
                        lecturerId = existingLecturer.LecturerId;
                    }
                    else 
                    {
                        // Tạo một ID mới ngắn hơn 10 ký tự
                        string newLecturerId = "L" + Guid.NewGuid().ToString().Substring(0, 8);
                        
                        // Tạo bản ghi Lecturer mới
                        var lecturer = new Lecturer
                        {
                            LecturerId = newLecturerId,
                            FullName = teacher.FullName ?? "Chưa cập nhật",
                            Name = teacher.FullName?.Split(' ').LastOrDefault() ?? "Giảng viên",
                            Email = teacher.Email,
                            Phone = "Chưa cập nhật",
                            Department = "Chưa cập nhật",
                            UserId = teacher.Id
                        };
                        
                        _context.Lecturers.Add(lecturer);
                        await _context.SaveChangesAsync();
                        
                        lecturerId = newLecturerId;
                    }
                }

                // Kiểm tra hoặc tạo Class mới
                int classId = 1; // Default classId
                var defaultClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassID == classId);
                
                if (defaultClass == null)
                {
                    // Tạo một class mặc định nếu không tồn tại
                    var newClass = new Class
                    {
                        ClassID = classId,
                        ClassName = "Lớp mặc định", 
                        ClassSize = 40
                    };
                    
                    _context.Classes.Add(newClass);
                    await _context.SaveChangesAsync();
                }
                
                // Create a new timetable entry
                var timetable = new Timetable
                {
                    CourseCode = courseCode.Length > 20 ? courseCode.Substring(0, 20) : courseCode, // Đảm bảo CourseCode không quá dài
                    LecturerId = lecturerId, // Sử dụng lecturerId đã được xử lý
                    RoomID = roomId.Length > 20 ? roomId.Substring(0, 20) : roomId, // Đảm bảo RoomID không quá dài
                    DayOfWeek = dayOfWeek,
                    StartPeriod = startPeriod,
                    NumPeriods = numPeriods,
                    DateStart = startDate,
                    DateEnd = endDate,
                    SemesterPhase = "A", // Default semester phase
                    Type = "LT", // Default type (LT for lecture)
                    ClassID = classId, // Sử dụng classId đã kiểm tra
                    Notes = $"Phân công bởi Admin vào {DateTime.Now:dd/MM/yyyy}",
                    ApplicationUserId = teacher.Id // Đảm bảo liên kết với tài khoản giảng viên
                };

                _context.Timetables.Add(timetable);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Đã phân công giảng viên {teacher.FullName} giảng dạy khóa học {course.CourseName}";
            }
            catch (Exception ex)
            {
                // Ghi log chi tiết của lỗi
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "Không có inner exception";
                _logger.LogError(ex, $"Lỗi khi phân công giảng viên. Chi tiết: {innerException}");
                
                TempData["ErrorMessage"] = $"Lỗi khi phân công giảng viên: {ex.Message}. Chi tiết: {innerException}. Vui lòng kiểm tra dữ liệu nhập vào.";
            }
            
            return RedirectToAction(nameof(CourseDetails), new { id = courseCode });
        }

        // GET: Admin/SearchSchedule
        public async Task<IActionResult> SearchSchedule()
        {
            ViewBag.Lecturers = new SelectList(_context.Lecturers.OrderBy(l => l.FullName), "LecturerId", "FullName");
            ViewBag.Courses = new SelectList(_context.Courses.OrderBy(c => c.CourseName), "CourseCode", "CourseName");
            ViewBag.Rooms = new SelectList(_context.Rooms.OrderBy(r => r.RoomName), "RoomID", "RoomName");
            ViewBag.Days = new SelectList(new[] 
            { 
                new { Value = 2, Text = "Thứ 2" },
                new { Value = 3, Text = "Thứ 3" },
                new { Value = 4, Text = "Thứ 4" },
                new { Value = 5, Text = "Thứ 5" },
                new { Value = 6, Text = "Thứ 6" },
                new { Value = 7, Text = "Thứ 7" },
                new { Value = 8, Text = "Chủ nhật" }
            }, "Value", "Text");
            
            // Lấy danh sách tất cả các học kỳ độc đáo đang có trong hệ thống
            var existingSemesters = await _context.Timetables
                .Where(t => !string.IsNullOrEmpty(t.SemesterPhase))
                .Select(t => t.SemesterPhase)
                .Distinct()
                .ToListAsync();
            
            // Tạo danh sách các học kỳ mặc định nếu không có dữ liệu
            var semesterList = new List<object>
            { 
                new { Value = "A", Text = "Học kỳ A" },
                new { Value = "B", Text = "Học kỳ B" }
            };
            
            // Thêm các học kỳ tìm thấy từ dữ liệu vào danh sách
            foreach (var semester in existingSemesters)
            {
                if (semester != "A" && semester != "B")
                {
                    semesterList.Add(new { Value = semester, Text = $"Học kỳ {semester}" });
                }
            }
            
            ViewBag.SemesterPhases = new SelectList(semesterList, "Value", "Text");
            
            // Thêm năm học
            var years = await _context.Timetables
                .Where(t => t.DateStart.HasValue)
                .Select(t => t.DateStart.Value.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
                
            ViewBag.Years = new SelectList(years.Select(y => new { Value = y, Text = $"Năm {y}" }), "Value", "Text");
            
            return View();
        }

        // POST: Admin/SearchSchedule
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> SearchSchedule(string lecturerId, string courseCode, string roomId, int? dayOfWeek, string semesterPhase, int? year)
        {
            var query = _context.Timetables
                .AsNoTracking()
                .Include(t => t.Course)
                .Include(t => t.Class)
                .Include(t => t.Room)
                .Include(t => t.Lecturer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(lecturerId))
            {
                query = query.Where(t => t.LecturerId == lecturerId);
            }

            if (!string.IsNullOrEmpty(courseCode))
            {
                query = query.Where(t => t.CourseCode == courseCode);
            }

            if (!string.IsNullOrEmpty(roomId))
            {
                query = query.Where(t => t.RoomID == roomId);
            }

            if (dayOfWeek.HasValue)
            {
                query = query.Where(t => t.DayOfWeek == dayOfWeek.Value);
            }

            if (!string.IsNullOrEmpty(semesterPhase))
            {
                query = query.Where(t => t.SemesterPhase == semesterPhase);
            }
            
            if (year.HasValue)
            {
                query = query.Where(t => t.DateStart.HasValue && t.DateStart.Value.Year == year.Value);
            }

            var timetables = await query.OrderBy(t => t.DayOfWeek)
                                        .ThenBy(t => t.StartPeriod)
                                        .ToListAsync();

            return PartialView("_TimetableSearchResults", timetables);
        }

        // GET: Admin/CreateAdmin
        public IActionResult CreateAdmin()
        {
            var model = new RegisterViewModel();
            // Pre-select the Administrator role and hide the dropdown
            model.Role = "Administrator";
            return View(model);
        }

        // POST: Admin/CreateAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdmin(RegisterViewModel model)
        {
            // Ensure the role is always Administrator
            model.Role = "Administrator";
            
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Role = "Administrator",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Administrator");
                    TempData["SuccessMessage"] = "Tài khoản quản trị viên mới đã được tạo thành công!";
                    return RedirectToAction(nameof(AdminUsers));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // Phương thức quản trị để đăng ký khóa học cho sinh viên student0
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AddDefaultCoursesForStudent0()
        {
            // Tìm người dùng student0@university.com
            var student = await _userManager.FindByEmailAsync("student0@university.com");
            if (student == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản student0@university.com";
                return RedirectToAction("Index", "Home");
            }

            // Lấy 3 khóa học đầu tiên từ cơ sở dữ liệu
            var courses = await _context.Courses.Take(3).ToListAsync();
            int count = 0;

            foreach (var course in courses)
            {
                // Kiểm tra xem sinh viên đã đăng ký khóa học này chưa
                var existingEnrollment = await _context.CourseEnrollments
                    .FirstOrDefaultAsync(ce => ce.CourseCode == course.CourseCode && ce.StudentId == student.Id);

                if (existingEnrollment == null)
                {
                    // Thêm đăng ký mới
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
            return RedirectToAction("Index", "Home");
        }

        // POST: Admin/LinkTeacherToLecturer
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> LinkTeacherToLecturer()
        {
            // Đếm số lượng bản ghi được cập nhật
            int updatedCount = 0;
            int createdCount = 0;
            
            try
            {
                // Lấy tất cả giáo viên - chỉ lấy người dùng có vai trò Teacher
                var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
                
                foreach (var teacher in teachers)
                {
                    // Tìm bản ghi Lecturer tương ứng theo email
                    var lecturer = await _context.Lecturers
                        .FirstOrDefaultAsync(l => l.Email == teacher.Email);
                    
                    if (lecturer != null)
                    {
                        // Đã có bản ghi lecturer, cập nhật UserId
                        if (string.IsNullOrEmpty(lecturer.UserId))
                        {
                            lecturer.UserId = teacher.Id;
                            _context.Update(lecturer);
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // Chưa có bản ghi lecturer, tạo mới
                        string newLecturerId = "L" + Guid.NewGuid().ToString().Substring(0, 8);
                        
                        var newLecturer = new Lecturer
                        {
                            LecturerId = newLecturerId,
                            FullName = teacher.FullName ?? "Chưa cập nhật",
                            Name = teacher.FullName?.Split(' ').LastOrDefault() ?? "Giảng viên",
                            Email = teacher.Email,
                            Phone = "Chưa cập nhật",
                            Department = "Chưa cập nhật",
                            UserId = teacher.Id
                        };
                        
                        _context.Lecturers.Add(newLecturer);
                        createdCount++;
                    }
                }
                
                // Lưu thay đổi
                await _context.SaveChangesAsync();
                
                // Bây giờ cập nhật các bản ghi timetable
                // Tìm tất cả các bản ghi timetable có ApplicationUserId nhưng không có LecturerId
                var timetables = await _context.Timetables
                    .Include(t => t.ApplicationUser)
                    .Where(t => !string.IsNullOrEmpty(t.ApplicationUserId))
                    .ToListAsync();
                
                // Lấy danh sách ID của giáo viên để kiểm tra
                var teacherIds = teachers.Select(t => t.Id).ToList();
                
                foreach (var timetable in timetables)
                {
                    if (timetable.ApplicationUser != null)
                    {
                        // Kiểm tra xem ApplicationUserId có thuộc về giáo viên không
                        if (teacherIds.Contains(timetable.ApplicationUserId))
                        {
                            // Tìm bản ghi lecturer cho user này
                            var lecturer = await _context.Lecturers
                                .FirstOrDefaultAsync(l => l.UserId == timetable.ApplicationUserId);
                            
                            if (lecturer != null && (string.IsNullOrEmpty(timetable.LecturerId) || timetable.LecturerId != lecturer.LecturerId))
                            {
                                timetable.LecturerId = lecturer.LecturerId;
                                _context.Update(timetable);
                                updatedCount++;
                            }
                        }
                    }
                }
                
                // Lưu thay đổi
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Đã liên kết giảng viên thành công! Tạo mới: {createdCount}, Cập nhật: {updatedCount}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi liên kết giảng viên: {ex.Message}";
            }
            
            return RedirectToAction("SearchSchedule");
        }

        // POST: Admin/AutoLinkLecturersByPattern
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AutoLinkLecturersByPattern()
        {
            int updatedCount = 0;
            int notFoundCount = 0;
            int skippedCount = 0;
            List<string> errorMessages = new List<string>();
            
            try
            {
                // Lấy tất cả bản ghi lecturer với mã bắt đầu bằng L và theo sau là số
                var lecturers = await _context.Lecturers
                    .Where(l => l.LecturerId.StartsWith("L") && l.LecturerId.Length >= 4)
                    .ToListAsync();
                
                // Lấy danh sách giáo viên để kiểm tra vai trò
                var teacherUsers = await _userManager.GetUsersInRoleAsync("Teacher");
                var teacherEmails = teacherUsers.Select(t => t.Email.ToLower()).ToList();
                
                foreach (var lecturer in lecturers)
                {
                    try
                    {
                        // Trích xuất số từ mã giảng viên (ví dụ: L001 -> 1, L002 -> 2)
                        if (int.TryParse(lecturer.LecturerId.Substring(1).TrimStart('0'), out int lecturerNumber))
                        {
                            // Tạo email tương ứng
                            string expectedEmail = $"lecturer{lecturerNumber}@university.com";
                            
                            // Tìm người dùng có email này
                            var user = await _userManager.FindByEmailAsync(expectedEmail);
                            
                            if (user != null)
                            {
                                // Kiểm tra xem user có phải là giáo viên không
                                if (teacherEmails.Contains(user.Email.ToLower()))
                                {
                                    // Cập nhật thông tin lecturer
                                    lecturer.UserId = user.Id;
                                    lecturer.Email = expectedEmail;
                                    lecturer.FullName = user.FullName ?? "Chưa cập nhật";
                                    lecturer.Name = user.FullName?.Split(' ').LastOrDefault() ?? "Giảng viên";
                                    
                                    _context.Update(lecturer);
                                    
                                    // Cập nhật các bản ghi timetable liên quan
                                    var timetables = await _context.Timetables
                                        .Where(t => t.LecturerId == lecturer.LecturerId)
                                        .ToListAsync();
                                        
                                    foreach (var timetable in timetables)
                                    {
                                        timetable.ApplicationUserId = user.Id;
                                        _context.Update(timetable);
                                    }
                                    
                                    updatedCount++;
                                }
                                else
                                {
                                    skippedCount++;
                                    errorMessages.Add($"Bỏ qua tài khoản {expectedEmail} vì không phải là giáo viên");
                                }
                            }
                            else
                            {
                                notFoundCount++;
                                errorMessages.Add($"Không tìm thấy tài khoản {expectedEmail} cho mã giảng viên {lecturer.LecturerId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Lỗi xử lý mã {lecturer.LecturerId}: {ex.Message}");
                    }
                }
                
                // Lưu tất cả thay đổi
                await _context.SaveChangesAsync();
                
                if (updatedCount > 0)
                {
                    TempData["SuccessMessage"] = $"Đã liên kết thành công {updatedCount} giảng viên theo quy tắc L00x -> lecturerX@university.com";
                }
                
                if (notFoundCount > 0 || skippedCount > 0)
                {
                    TempData["WarningMessage"] = $"Có {notFoundCount} mã giảng viên không tìm thấy tài khoản email tương ứng và {skippedCount} tài khoản bị bỏ qua vì không phải là giáo viên";
                }
                
                if (errorMessages.Count > 0)
                {
                    TempData["ErrorDetails"] = string.Join("<br>", errorMessages.Take(10));
                    if (errorMessages.Count > 10)
                    {
                        TempData["ErrorDetails"] += $"<br>... và {errorMessages.Count - 10} lỗi khác";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi liên kết giảng viên: {ex.Message}";
            }
            
            return RedirectToAction("SearchSchedule");
        }

        // POST: Admin/LinkSpecificLecturers
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> LinkSpecificLecturers()
        {
            int updatedCount = 0;
            int skippedCount = 0;
            List<string> errorMessages = new List<string>();
            
            try
            {
                // Tạo ánh xạ giữa mã giảng viên và email
                var lecturerMap = new Dictionary<string, string>
                {
                    { "L001", "lecturer1@university.com" },
                    { "L002", "lecturer2@university.com" }
                    // Có thể thêm các cặp mã - email khác nếu cần
                };
                
                foreach (var mapping in lecturerMap)
                {
                    string lecturerId = mapping.Key;
                    string email = mapping.Value;
                    
                    // Tìm người dùng có email tương ứng
                    var user = await _userManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        errorMessages.Add($"Không tìm thấy tài khoản {email}");
                        continue;
                    }
                    
                    // Kiểm tra xem người dùng có vai trò là Teacher không
                    if (!await _userManager.IsInRoleAsync(user, "Teacher"))
                    {
                        errorMessages.Add($"Tài khoản {email} không phải là giáo viên. Chỉ tài khoản giáo viên mới được liên kết.");
                        skippedCount++;
                        continue;
                    }
                    
                    // Tìm bản ghi lecturer với mã tương ứng
                    var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == lecturerId);
                    if (lecturer == null)
                    {
                        errorMessages.Add($"Không tìm thấy giảng viên có mã {lecturerId}");
                        continue;
                    }
                    
                    // Cập nhật thông tin
                    lecturer.UserId = user.Id;
                    lecturer.Email = email;
                    lecturer.FullName = user.FullName ?? "Chưa cập nhật";
                    lecturer.Name = user.FullName?.Split(' ').LastOrDefault() ?? "Giảng viên";
                    
                    _context.Update(lecturer);
                    updatedCount++;
                    
                    // Cập nhật các bản ghi timetable liên quan
                    var timetables = await _context.Timetables
                        .Where(t => t.LecturerId == lecturerId)
                        .ToListAsync();
                        
                    foreach (var timetable in timetables)
                    {
                        timetable.ApplicationUserId = user.Id;
                        _context.Update(timetable);
                        updatedCount++;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                if (updatedCount > 0)
                {
                    TempData["SuccessMessage"] = $"Đã liên kết thành công {updatedCount} bản ghi";
                }
                
                if (skippedCount > 0)
                {
                    TempData["WarningMessage"] = $"Có {skippedCount} tài khoản bị bỏ qua vì không phải là giáo viên";
                }
                
                if (errorMessages.Count > 0)
                {
                    TempData["ErrorDetails"] = string.Join("<br>", errorMessages);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi liên kết giảng viên: {ex.Message}";
            }
            
            return RedirectToAction("SearchSchedule");
        }

        private bool CourseExists(string id)
        {
            return _context.Courses.Any(e => e.CourseCode == id);
        }

        private bool RoomExists(string id)
        {
            return _context.Rooms.Any(e => e.RoomID == id);
        }
    }
} 