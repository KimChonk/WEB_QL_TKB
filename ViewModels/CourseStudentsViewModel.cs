using System.Collections.Generic;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.ViewModels
{
    public class CourseStudentsViewModel
    {
        public Course Course { get; set; }
        public List<StudentViewModel> Students { get; set; } = new List<StudentViewModel>();
    }
} 