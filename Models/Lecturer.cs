using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimetableManagementApp.Models;

public class Lecturer
{
    [Key]
    [StringLength(10)]
    public string LecturerId { get; set; }

    [StringLength(100)]
    public string FullName { get; set; }

    [StringLength(50)]
    public string Name { get; set; }

    [StringLength(50)]
    public string Email { get; set; }

    [StringLength(15)]
    public string Phone { get; set; }

    [StringLength(50)]
    public string Department { get; set; }

    [ForeignKey("User")]
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Navigation property for timetables
    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
