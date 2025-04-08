using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models
{
    public class CourseEnrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        [Required]
        [StringLength(20)]
        [ForeignKey("Course")]
        public string CourseCode { get; set; }

        [Required]
        [ForeignKey("Student")]
        public string StudentId { get; set; }

        [Required]
        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual Course Course { get; set; }
        
        public virtual ApplicationUser Student { get; set; }
    }
} 