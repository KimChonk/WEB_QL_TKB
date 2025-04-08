using System;
using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class AttendanceViewModel
    {
        public Timetable Timetable { get; set; }
        public string AttendanceCode { get; set; }
        public string QRCodeUrl { get; set; }
        public DateTime CodeGeneratedTime { get; set; }
        public List<ApplicationUser> EnrolledStudents { get; set; }
        public List<Attendance> Attendances { get; set; }
    }
    
    public class AttendanceManagementViewModel
    {
        public Timetable Timetable { get; set; }
        public DateTime AttendanceDate { get; set; }
        public List<Attendance> Attendances { get; set; }
        public List<ApplicationUser> EnrolledStudents { get; set; }
    }
    
    public class StudentAttendanceViewModel
    {
        public Timetable Timetable { get; set; }
        public Course Course { get; set; }
        public Attendance Attendance { get; set; }
        public bool CanAttend { get; set; }
        public bool IsAttended { get; set; }
        public DateTime? AttendanceTime { get; set; }
        public string AttendanceMethod { get; set; }
        public List<Attendance> AttendanceHistory { get; set; }
    }
    
    public class AttendanceHistoryViewModel
    {
        public Course Course { get; set; }
        public List<Attendance> Attendances { get; set; }
        public List<Timetable> Timetables { get; set; }
        public int TotalSessions { get; set; }
        public int PresentSessions { get; set; }
        public decimal AttendancePercentage => TotalSessions > 0 ? (decimal)PresentSessions / TotalSessions * 100 : 0;
    }
} 