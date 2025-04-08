using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class LecturerScheduleViewModel
    {
        public required Lecturer Lecturer { get; set; }
        public required List<Timetable> Schedules { get; set; }
    }
} 