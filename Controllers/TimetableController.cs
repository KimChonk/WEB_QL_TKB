using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TimetableManagementApp.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using TimetableManagementApp.ViewModels;

namespace TimetableManagementApp.Controllers
{
    [Authorize]
    public class TimetableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimetableController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Timetable
        public async Task<IActionResult> Index()
        {
            var timetables = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Lecturer)
                .Include(t => t.Room)
                .Include(t => t.Class)
                .ToListAsync();
            return View(timetables);
        }

        // GET: Timetable/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timetable = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Lecturer)
                .Include(t => t.Room)
                .Include(t => t.Class)
                .FirstOrDefaultAsync(m => m.TimetableID == id);
                
            if (timetable == null)
            {
                return NotFound();
            }

            return View(timetable);
        }

        // GET: Timetable/Create
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create()
        {
            var courses = await _context.Courses.ToListAsync();
            var lecturers = await _context.Lecturers.ToListAsync();
            var rooms = await _context.Rooms.ToListAsync();
            var classes = await _context.Classes.ToListAsync();

            ViewBag.Courses = courses;
            ViewBag.Lecturers = lecturers;
            ViewBag.Rooms = rooms;
            ViewBag.Classes = classes;

            return View();
        }

        // POST: Timetable/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create([Bind("CourseCode,ClassID,LecturerId,RoomID,DayOfWeek,StartPeriod,NumPeriods,DateStart,DateEnd,Type,SemesterPhase")] Timetable timetable)
        {
            if (ModelState.IsValid)
            {
                _context.Add(timetable);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var courses = await _context.Courses.ToListAsync();
            var lecturers = await _context.Lecturers.ToListAsync();
            var rooms = await _context.Rooms.ToListAsync();
            var classes = await _context.Classes.ToListAsync();

            ViewBag.Courses = courses;
            ViewBag.Lecturers = lecturers;
            ViewBag.Rooms = rooms;
            ViewBag.Classes = classes;

            return View(timetable);
        }

        // GET: Timetable/Edit/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timetable = await _context.Timetables.FindAsync(id);
            if (timetable == null)
            {
                return NotFound();
            }

            var courses = await _context.Courses.ToListAsync();
            var lecturers = await _context.Lecturers.ToListAsync();
            var rooms = await _context.Rooms.ToListAsync();
            var classes = await _context.Classes.ToListAsync();

            ViewBag.Courses = courses;
            ViewBag.Lecturers = lecturers;
            ViewBag.Rooms = rooms;
            ViewBag.Classes = classes;

            return View(timetable);
        }

        // POST: Timetable/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("TimetableID,CourseCode,ClassID,LecturerId,RoomID,DayOfWeek,StartPeriod,NumPeriods,DateStart,DateEnd,Type,SemesterPhase")] Timetable timetable)
        {
            if (id != timetable.TimetableID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(timetable);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TimetableExists(timetable.TimetableID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            var courses = await _context.Courses.ToListAsync();
            var lecturers = await _context.Lecturers.ToListAsync();
            var rooms = await _context.Rooms.ToListAsync();
            var classes = await _context.Classes.ToListAsync();

            ViewBag.Courses = courses;
            ViewBag.Lecturers = lecturers;
            ViewBag.Rooms = rooms;
            ViewBag.Classes = classes;

            return View(timetable);
        }

        // GET: Timetable/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timetable = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Lecturer)
                .Include(t => t.Room)
                .Include(t => t.Class)
                .FirstOrDefaultAsync(m => m.TimetableID == id);
                
            if (timetable == null)
            {
                return NotFound();
            }

            return View(timetable);
        }

        // POST: Timetable/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var timetable = await _context.Timetables.FindAsync(id);
            if (timetable != null)
            {
                _context.Timetables.Remove(timetable);
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool TimetableExists(int id)
        {
            return _context.Timetables.Any(e => e.TimetableID == id);
        }
    }
} 