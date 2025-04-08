using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class StudentTimetableViewModel
    {
        public ApplicationUser Student { get; set; }
        public List<Course> Courses { get; set; }
        public List<Timetable> Timetables { get; set; }
    }
} 