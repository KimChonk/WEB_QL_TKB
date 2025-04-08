using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace TimetableManagementApp.ViewModels;

public class RegisterViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; }

    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
    
    [Required]
    [Display(Name = "Vai trò")]
    public string Role { get; set; }
    
    public List<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "Student", Text = "Sinh viên" },
        new SelectListItem { Value = "Teacher", Text = "Giáo viên" },
        new SelectListItem { Value = "Administrator", Text = "Quản trị viên" }
    };
} 