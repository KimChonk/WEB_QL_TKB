using System;
using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class CourseEnrollmentViewModel
    {
        public Course Course { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public List<Timetable> Timetables { get; set; } = new List<Timetable>();
    }
} 