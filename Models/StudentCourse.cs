using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models;

public class StudentCourse
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string StudentId { get; set; } = null!;

    [Required]
    public string CourseCode { get; set; } = null!;

    public DateTime EnrollmentDate { get; set; } = DateTime.Now;

    [ForeignKey("StudentId")]
    public virtual ApplicationUser Student { get; set; } = null!;

    [ForeignKey("CourseCode")]
    public virtual Course Course { get; set; } = null!;
} 