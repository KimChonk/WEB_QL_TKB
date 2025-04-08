using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models;

public class Timetable
{
    [Key]
    public int TimetableID { get; set; }

    [StringLength(20)]
    public string CourseCode { get; set; }

    // ClassID is required and shouldn't be nullable
    public int ClassID { get; set; }

    // Thay đổi từ int sang string
    [StringLength(50)]
    public string? ClassIDInt { get; set; }

    [StringLength(10)]
    public string SemesterPhase { get; set; }
    
    public int? DayOfWeek { get; set; }
    
    public int? StartPeriod { get; set; }
    
    public int? NumPeriods { get; set; }
    
    [StringLength(20)]
    public string RoomID { get; set; }
    
    // Reference to the Lecturer entity - just a foreign key, NOT linked to AspNetUsers
    [StringLength(10)]
    [ForeignKey("Lecturer")]
    public string LecturerId { get; set; }
    
    // Duplicate LecturerId field for compatibility with existing database
    [StringLength(10)]
    [ForeignKey("Lecturer1")]
    public string? LecturerId1 { get; set; }
    
    // Reference to the ApplicationUser (teacher) for this timetable
    [ForeignKey("ApplicationUser")]
    public string? ApplicationUserId { get; set; }
    
    public DateTime? DateStart { get; set; }
    
    public DateTime? DateEnd { get; set; }
    
    public int? GroupID { get; set; }
    
    [StringLength(5)]
    public string Type { get; set; }

    // Add Notes property to match database schema
    public string Notes { get; set; } = "Imported from Excel";

    // Add IsActive property with default value
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey("ClassID")]
    public virtual Class Class { get; set; }

    [ForeignKey("CourseCode")]
    public virtual Course Course { get; set; }

    public virtual Lecturer Lecturer { get; set; }
    
    public virtual Lecturer? Lecturer1 { get; set; }
    
    public virtual ApplicationUser? ApplicationUser { get; set; }

    [ForeignKey("RoomID")]
    public virtual Room Room { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
