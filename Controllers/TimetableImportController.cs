using Microsoft.AspNetCore.Mvc;
using TimetableManagementApp.Services;
using TimetableManagementApp.Models;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.Mvc.Core;
using Microsoft.Extensions.Logging;

namespace TimetableManagementApp.Controllers;

public class TimetableImportController : Controller
{
    private readonly TimetableImportService _importService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TimetableImportController> _logger;

    public TimetableImportController(TimetableImportService importService, ApplicationDbContext context, ILogger<TimetableImportController> logger)
    {
        _importService = importService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return RedirectToAction(nameof(Import));
        }

        if (!file.FileName.EndsWith(".xlsx"))
        {
            TempData["Error"] = "Please upload an Excel file (.xlsx)";
            return RedirectToAction(nameof(Import));
        }

        using (var stream = file.OpenReadStream())
        {
            var (success, message, importedTimetables) = await _importService.ImportFromExcel(stream);
            
            if (success)
            {
                TempData["Success"] = message;
            }
            else
            {
                TempData["Error"] = message;
            }
        }

        return RedirectToAction(nameof(Import));
    }

    [HttpGet]
    public async Task<IActionResult> ViewTimetables(int? page, string searchString, string currentFilter)
    {
        try
        {
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            int pageSize = 10;
            int pageNumber = (page ?? 1);

            // Truy vấn dữ liệu tối ưu
            var timetables = await _context.Timetables
                .AsNoTracking()
                .Include(t => t.Course)
                .Include(t => t.Class)
                .Include(t => t.Room)
                .Include(t => t.Lecturer)
                .OrderByDescending(t => t.TimetableID)
                .ToListAsync();

            _logger.LogInformation($"Fetched {timetables.Count} timetables from database");

            // Áp dụng tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                _logger.LogInformation($"Applying search filter: {searchString}");
                timetables = timetables.Where(t =>
                    (t.CourseCode != null && t.CourseCode.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Course != null && t.Course.CourseName != null && t.Course.CourseName.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Lecturer != null && t.Lecturer.FullName != null && t.Lecturer.FullName.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Room != null && t.Room.RoomName != null && t.Room.RoomName.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (t.ClassIDInt != null && t.ClassIDInt.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                ).ToList();
                _logger.LogInformation($"After filter: {timetables.Count} records match");
            }

            // Hiển thị thông tin debug
            foreach (var timetable in timetables.Take(5))
            {
                _logger.LogDebug($"Sample timetable - ID: {timetable.TimetableID}, ClassID: {timetable.ClassID}, ClassIDInt: {timetable.ClassIDInt}");
            }

            var pagedList = new PagedList<Timetable>(timetables, pageNumber, pageSize);
            return View(pagedList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ViewTimetables action");
            TempData["Error"] = $"Lỗi khi tải danh sách thời khóa biểu: {ex.Message}";
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner exception in ViewTimetables action");
                TempData["Error"] += $" Chi tiết: {ex.InnerException.Message}";
            }
            
            return View(new PagedList<Timetable>(new List<Timetable>(), 1, 10));
        }
    }
} 