using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TimetableManagementApp.Models;

public class Class
{
    [Key]
    public int ClassID { get; set; }

    [Required]
    [StringLength(50)]
    public string ClassName { get; set; }

    public int? ClassSize { get; set; }

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
