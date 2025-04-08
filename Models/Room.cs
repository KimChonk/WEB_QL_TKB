using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TimetableManagementApp.Models;

public class Room
{
    [Key]
    [StringLength(20)]
    public string RoomID { get; set; }

    [Required]
    [StringLength(50)]
    public string RoomName { get; set; }

    public int? Capacity { get; set; }

    [StringLength(100)]
    public string Location { get; set; }

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
