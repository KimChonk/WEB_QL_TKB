using Microsoft.AspNetCore.Identity;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace TimetableManagementApp.Services;

public class StudentCourseImportService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StudentCourseImportService> _logger;

    public StudentCourseImportService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<StudentCourseImportService> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public async Task<StudentCourseImportResult> ImportStudentCoursesFromExcel(Stream fileStream)
    {
        var result = new StudentCourseImportResult();
        result.Errors = new List<string>();
        
        try
        {
            // Mở workbook Excel
            var workbook = new XSSFWorkbook(fileStream);
            var sheet = workbook.GetSheetAt(0); // Lấy sheet đầu tiên

            var rowCount = sheet.LastRowNum;
            if (rowCount < 1)
            {
                result.Success = false;
                result.Message = "File Excel không có dữ liệu";
                return result;
            }

            // Xử lý từng dòng trong file Excel (bắt đầu từ dòng thứ 2, bỏ qua header)
            for (int i = 1; i <= rowCount; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    var email = GetCellValueAsString(row.GetCell(0));
                    var courseCode = GetCellValueAsString(row.GetCell(1));
                    
                    // Kiểm tra thông tin bắt buộc
                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(courseCode))
                    {
                        result.Errors.Add($"Dòng {i+1}: Email hoặc mã khóa học không được để trống");
                        result.ErrorCount++;
                        continue;
                    }

                    // Đảm bảo email hợp lệ
                    if (!email.Contains("@"))
                    {
                        result.Errors.Add($"Dòng {i+1}: Email {email} không hợp lệ");
                        result.ErrorCount++;
                        continue;
                    }
                    
                    // Kiểm tra sinh viên tồn tại
                    var student = await _userManager.FindByEmailAsync(email);
                    if (student == null)
                    {
                        result.Errors.Add($"Dòng {i+1}: Sinh viên với email {email} không tồn tại trong hệ thống");
                        result.ErrorCount++;
                        continue;
                    }
                    
                    // Kiểm tra sinh viên có vai trò Student không
                    var isStudent = await _userManager.IsInRoleAsync(student, "Student");
                    if (!isStudent)
                    {
                        result.Errors.Add($"Dòng {i+1}: Người dùng {email} không phải là sinh viên");
                        result.ErrorCount++;
                        continue;
                    }

                    // Kiểm tra khóa học tồn tại
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseCode == courseCode);
                    if (course == null)
                    {
                        result.Errors.Add($"Dòng {i+1}: Khóa học với mã {courseCode} không tồn tại");
                        result.ErrorCount++;
                        continue;
                    }

                    // Kiểm tra sinh viên đã đăng ký khóa học chưa
                    var existingEnrollment = await _context.CourseEnrollments
                        .FirstOrDefaultAsync(e => e.CourseCode == courseCode && e.StudentId == student.Id);
                    
                    if (existingEnrollment != null)
                    {
                        result.Errors.Add($"Dòng {i+1}: Sinh viên {email} đã đăng ký khóa học {courseCode}");
                        result.ErrorCount++;
                        continue;
                    }

                    // Tạo đăng ký khóa học mới
                    var enrollment = new CourseEnrollment
                    {
                        StudentId = student.Id,
                        CourseCode = courseCode,
                        EnrollmentDate = DateTime.Now
                    };

                    _context.CourseEnrollments.Add(enrollment);
                    
                    // Thêm vào model StudentCourse
                    var studentCourse = new StudentCourse
                    {
                        StudentId = student.Id,
                        CourseCode = courseCode,
                        EnrollmentDate = DateTime.Now
                    };
                    
                    _context.StudentCourses.Add(studentCourse);
                    
                    await _context.SaveChangesAsync();
                    result.SuccessCount++;
                    
                    // Lưu thông tin đăng ký thành công
                    if (!result.ImportedEnrollments.ContainsKey(email))
                    {
                        result.ImportedEnrollments[email] = new List<string>();
                    }
                    
                    result.ImportedEnrollments[email].Add(courseCode);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Dòng {i+1}: Lỗi xử lý - {ex.Message}");
                    result.ErrorCount++;
                    _logger.LogError($"Lỗi xử lý dữ liệu dòng {i+1}: {ex.Message}");
                }
            }

            result.Success = result.SuccessCount > 0;
            if (string.IsNullOrEmpty(result.Message)) {
                result.Message = $"Đã đăng ký {result.SuccessCount} khóa học thành công, {result.ErrorCount} lỗi";
            } else {
                result.Message += $". Đã đăng ký {result.SuccessCount} khóa học thành công, {result.ErrorCount} lỗi";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Lỗi xử lý file Excel: {ex.Message}";
            _logger.LogError($"Lỗi xử lý file Excel: {ex.Message}");
            return result;
        }
    }

    private string GetCellValueAsString(ICell cell)
    {
        if (cell == null) return string.Empty;

        switch (cell.CellType)
        {
            case CellType.Numeric:
                return cell.NumericCellValue.ToString();
            case CellType.String:
                return cell.StringCellValue.Trim();
            case CellType.Boolean:
                return cell.BooleanCellValue.ToString();
            case CellType.Formula:
                switch (cell.CachedFormulaResultType)
                {
                    case CellType.Numeric:
                        return cell.NumericCellValue.ToString();
                    case CellType.String:
                        return cell.StringCellValue.Trim();
                    default:
                        return string.Empty;
                }
            default:
                return string.Empty;
        }
    }
}

public class StudentCourseImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    
    // Lưu trữ thông tin đăng ký khóa học (email -> danh sách khóa học)
    public Dictionary<string, List<string>> ImportedEnrollments { get; set; } = new Dictionary<string, List<string>>();
} 