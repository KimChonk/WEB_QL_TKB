using System;
using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class CourseDetailsViewModel
    {
        public Course Course { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public List<Timetable> Timetables { get; set; } = new List<Timetable>();
        public int StudentCount { get; set; }
        public List<EnrolledStudentViewModel> EnrolledStudents { get; set; } = new List<EnrolledStudentViewModel>();
    }

    public class EnrolledStudentViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public System.DateTime EnrollmentDate { get; set; }
    }
} 