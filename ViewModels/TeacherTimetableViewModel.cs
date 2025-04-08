using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class TeacherTimetableViewModel
    {
        public ApplicationUser Teacher { get; set; }
        public List<Course> Courses { get; set; } = new List<Course>();
        public Dictionary<string, List<Timetable>> CourseTimetables { get; set; } = new Dictionary<string, List<Timetable>>();
        public List<Timetable> AllTimetables { get; set; } = new List<Timetable>();
    }
} 