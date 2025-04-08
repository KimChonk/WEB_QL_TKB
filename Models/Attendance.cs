using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int TimetableID { get; set; }

        [StringLength(450)]
        public string StudentId { get; set; }

        [Required]
        public DateTime AttendanceDate { get; set; }

        [Required]
        public bool IsPresent { get; set; }

        [StringLength(500)]
        public string Note { get; set; }

        [ForeignKey("TimetableID")]
        public virtual Timetable Timetable { get; set; }

        [ForeignKey("StudentId")]
        public virtual ApplicationUser Student { get; set; }

        // Trường mới cho chức năng điểm danh
        [StringLength(4)]
        public string? AttendanceCode { get; set; } // Mã 4 số điểm danh

        public DateTime? CodeGeneratedTime { get; set; } // Thời gian tạo mã

        public DateTime? AttendanceTime { get; set; } // Thời gian sinh viên điểm danh

        public string? AttendanceMethod { get; set; } // Phương thức điểm danh: "QR", "Code", "Manual"
    }
} 