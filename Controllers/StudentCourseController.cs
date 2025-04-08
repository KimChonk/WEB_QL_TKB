using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TimetableManagementApp.Services;
using TimetableManagementApp.ViewModels;
using TimetableManagementApp.Models;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace TimetableManagementApp.Controllers;

[Authorize(Roles = "Administrator")]
public class StudentCourseController : Controller
{
    private readonly StudentCourseImportService _importService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentCourseController(
        StudentCourseImportService importService, 
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _importService = importService;
        _context = context;
        _userManager = userManager;
    }

    // GET: StudentCourse/Import
    public IActionResult Import()
    {
        var model = new StudentCourseImportViewModel();
        return View(model);
    }

    // POST: StudentCourse/Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = 104857600)]
    [RequestSizeLimit(104857600)] // 100MB
    public async Task<IActionResult> Import(StudentCourseImportViewModel model)
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

        try
        {
            using (var stream = model.ExcelFile.OpenReadStream())
            {
                var result = await _importService.ImportStudentCoursesFromExcel(stream);
                
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                    // Lưu thông tin khóa học vừa đăng ký, giới hạn số lượng hiển thị
                    if (result.ImportedEnrollments != null && result.ImportedEnrollments.Count > 0)
                    {
                        // Giới hạn số lượng dữ liệu để không vượt quá limit của TempData
                        var limitedEnrollments = result.ImportedEnrollments
                            .Take(20) // Chỉ lấy 20 học sinh đầu tiên
                            .ToDictionary(x => x.Key, x => x.Value.Take(5).ToList()); // Mỗi học sinh chỉ hiển thị 5 khóa học

                        TempData["ImportedEnrollments"] = JsonSerializer.Serialize(limitedEnrollments, new JsonSerializerOptions
                        {
                            WriteIndented = false // Giảm kích thước JSON
                        });
                        
                        TempData["TotalImported"] = result.ImportedEnrollments.Count;
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                // Lưu danh sách lỗi chi tiết nếu có, giới hạn số lượng lỗi hiển thị
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    // Giới hạn số lượng lỗi
                    var limitedErrors = result.Errors.Take(20).ToList();
                    if (result.Errors.Count > 20)
                    {
                        limitedErrors.Add($"... và {result.Errors.Count - 20} lỗi khác");
                    }
                    
                    TempData["DetailedErrors"] = JsonSerializer.Serialize(limitedErrors, new JsonSerializerOptions
                    {
                        WriteIndented = false // Giảm kích thước JSON
                    });
                }
                
                return RedirectToAction(nameof(Import));
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Đã xảy ra lỗi: {ex.Message}");
            return View(model);
        }
    }

    // GET: StudentCourse/DownloadTemplate
    public IActionResult DownloadTemplate()
    {
        try
        {
            // Create a memory stream that will survive after the workbook is disposed
            var stream = new MemoryStream();
            
            // Create and populate the workbook
            using (var workbook = new NPOI.XSSF.UserModel.XSSFWorkbook())
            {
                var sheet = workbook.CreateSheet("Student_Courses");
                
                // Tạo header
                var headerRow = sheet.CreateRow(0);
                headerRow.CreateCell(0).SetCellValue("Email sinh viên");
                headerRow.CreateCell(1).SetCellValue("Mã khóa học");
                
                // Tạo một số dòng mẫu
                var exampleRow = sheet.CreateRow(1);
                exampleRow.CreateCell(0).SetCellValue("student0@university.com");
                exampleRow.CreateCell(1).SetCellValue("COURSE001");
                
                // Write to memory stream and complete the workbook
                workbook.Write(stream);
            } // workbook is disposed here, but stream is still open
            
            // Reset stream position and return the data
            stream.Position = 0;
            
            // Directly return the bytes instead of keeping stream open
            var bytes = stream.ToArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "student_course_template.xlsx");
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error creating template: {ex.Message}");
            return BadRequest("Không thể tạo tệp mẫu. Vui lòng thử lại sau.");
        }
    }
} 