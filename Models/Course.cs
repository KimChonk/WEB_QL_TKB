using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models;

public class Course
{
    [Key]
    [Required]
    [Display(Name = "Mã khóa học")]
    public string CourseCode { get; set; }

    [Required]
    [Display(Name = "Tên khóa học")]
    public string CourseName { get; set; }

    [Required]
    [Display(Name = "Số tín chỉ")]
    public int Credits { get; set; }

    [Required]
    [Display(Name = "Khoa/Bộ môn")]
    public string Department { get; set; }

    [Display(Name = "Mô tả")]
    public string Description { get; set; }

    // Navigation properties
    public virtual ICollection<StudentCourse> StudentCourses { get; set; }
    public virtual ICollection<Timetable> Timetables { get; set; }
    public virtual ICollection<CourseEnrollment> CourseEnrollments { get; set; }
}
