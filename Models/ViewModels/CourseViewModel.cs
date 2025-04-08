using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TimetableManagementApp.Models.ViewModels
{
    public class CourseViewModel
    {
        [Display(Name = "Mã khóa học")]
        public string CourseCode { get; set; }

        [Display(Name = "Tên khóa học")]
        public string CourseName { get; set; }

        [Display(Name = "Số tín chỉ")]
        public int Credits { get; set; }

        [Display(Name = "Khoa/Bộ môn")]
        public string Department { get; set; }

        [Display(Name = "Số sinh viên")]
        public int StudentCount { get; set; }
    }
} 