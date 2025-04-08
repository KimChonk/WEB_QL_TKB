using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace TimetableManagementApp.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? Role { get; set; }
    
    // Foreign key property to link with Lecturer
    public string? LecturerId { get; set; }
    
    // Navigation properties
    public virtual Lecturer? Lecturer { get; set; }
    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
    public virtual ICollection<StudentCourse> EnrolledCourses { get; set; } = new List<StudentCourse>();
    public virtual ICollection<CourseEnrollment> CourseEnrollments { get; set; } = new List<CourseEnrollment>();
} 