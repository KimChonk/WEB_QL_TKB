using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace TimetableManagementApp.ViewModels;

public class UserImportViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn file Excel")]
    [Display(Name = "File Excel")]
    public IFormFile ExcelFile { get; set; }
    
    [Required(ErrorMessage = "Vui lòng chọn loại tài khoản")]
    [Display(Name = "Loại tài khoản")]
    public string UserType { get; set; }
    
    [Display(Name = "Mặc định mật khẩu")]
    public string DefaultPassword { get; set; } = "Password@123";
}

public class UserImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    
    // Lưu trữ thông tin tài khoản và mật khẩu đã tạo (email -> password)
    public Dictionary<string, string> ImportedUsers { get; set; } = new Dictionary<string, string>();
} 