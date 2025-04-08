using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TimetableManagementApp.ViewModels
{
    public class AddStudentToCourseViewModel
    {
        [Required]
        [Display(Name = "Mã khóa học")]
        public string CourseCode { get; set; }

        [Required]
        [Display(Name = "Tên khóa học")]
        public string CourseName { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sinh viên")]
        [Display(Name = "Sinh viên")]
        public string SelectedStudentId { get; set; }

        public List<SelectListItem> Students { get; set; } = new List<SelectListItem>();
    }
} 