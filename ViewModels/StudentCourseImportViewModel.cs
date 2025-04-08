using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace TimetableManagementApp.ViewModels;

public class StudentCourseImportViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn file Excel")]
    [Display(Name = "File Excel")]
    public IFormFile ExcelFile { get; set; }
    
    [Display(Name = "Hướng dẫn")]
    public string Instructions => "File Excel phải có cột đầu tiên là Email sinh viên và cột thứ hai là Mã khóa học";
} 