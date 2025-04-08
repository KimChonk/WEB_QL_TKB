using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace TimetableManagementApp.Controllers
{
    [Authorize]
    public class LecturersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LecturersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Lecturers
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Lecturers.ToListAsync());
        }

        // GET: Lecturers/Details/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(m => m.LecturerId == id);
            if (lecturer == null)
            {
                return NotFound();
            }

            return View(lecturer);
        }

        // GET: Lecturers/Create
        [Authorize(Roles = "Administrator")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Lecturers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create([Bind("LecturerId,FullName,Email,Phone,Department")] Lecturer lecturer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(lecturer);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Error saving data. Please check that all fields are valid.");
                }
            }
            return View(lecturer);
        }

        // GET: Lecturers/Edit/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer == null)
            {
                return NotFound();
            }
            return View(lecturer);
        }

        // POST: Lecturers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(string id, [Bind("LecturerId,FullName,Email,Phone,Department")] Lecturer lecturer)
        {
            if (id != lecturer.LecturerId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(lecturer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LecturerExists(lecturer.LecturerId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Error saving data. Please check that all fields are valid.");
                    return View(lecturer);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(lecturer);
        }

        // GET: Lecturers/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecturer = await _context.Lecturers
                .Include(l => l.Timetables)
                .FirstOrDefaultAsync(m => m.LecturerId == id);
            if (lecturer == null)
            {
                return NotFound();
            }

            // Check if lecturer has related timetables
            ViewBag.HasRelatedRecords = lecturer.Timetables.Any();

            return View(lecturer);
        }

        // POST: Lecturers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var lecturer = await _context.Lecturers
                .Include(l => l.Timetables)
                .FirstOrDefaultAsync(m => m.LecturerId == id);
                
            if (lecturer == null)
            {
                return RedirectToAction(nameof(Index));
            }

            // Check if lecturer has related timetables
            if (lecturer.Timetables.Any())
            {
                ModelState.AddModelError("", "Cannot delete this lecturer because they are assigned to one or more timetable entries. Remove the lecturer from all timetables first.");
                ViewBag.HasRelatedRecords = true;
                return View(lecturer);
            }

            try
            {
                _context.Lecturers.Remove(lecturer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Cannot delete this lecturer because they are referenced in other records. Remove all references to this lecturer first.");
                return View(lecturer);
            }
        }

        // GET: Lecturers/Schedule
        public IActionResult Schedule()
        {
            ViewBag.Lecturers = new SelectList(_context.Lecturers.OrderBy(l => l.FullName), "LecturerId", "FullName");
            ViewBag.Years = new SelectList(Enumerable.Range(DateTime.Now.Year - 1, 3));
            ViewBag.Semesters = new SelectList(new[] { 1, 2, 3 });
            ViewBag.SemesterPhases = new SelectList(new[] { "A", "B" });
            return View();
        }

        // POST: Lecturers/Schedule
        [HttpPost]
        public async Task<IActionResult> Schedule(string? lecturerId, string? searchName, int? semester, int? year, string? semesterPhase)
        {
            try
            {
                var query = _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Room)
                    .Include(t => t.Class)
                    .Include(t => t.Lecturer)
                    .AsQueryable();

                // Search by lecturer ID or name
                if (!string.IsNullOrEmpty(lecturerId))
                {
                    query = query.Where(t => t.LecturerId == lecturerId);
                }
                else if (!string.IsNullOrEmpty(searchName))
                {
                    searchName = searchName.Trim();
                    query = query.Where(t => EF.Functions.Like(t.Lecturer.FullName, $"%{searchName}%"));
                }
                else
                {
                    ModelState.AddModelError("", "Please enter a lecturer name or select a lecturer from the list.");
                    PrepareViewBagForSchedule();
                    return View();
                }

                // Apply filters
                // if (semester.HasValue)
                // {
                //     query = query.Where(t => t.Semester == semester.Value);
                // }

                // if (year.HasValue)
                // {
                //     query = query.Where(t => t.AcademicYear == year.Value);
                // }

                if (!string.IsNullOrEmpty(semesterPhase))
                {
                    query = query.Where(t => t.SemesterPhase == semesterPhase);
                }

                var schedule = await query
                    .OrderBy(t => t.DayOfWeek)
                    .ThenBy(t => t.StartPeriod)
                    .ToListAsync();

                var groupedSchedule = schedule
                    .GroupBy(s => s.Lecturer!)
                    .Select(g => new LecturerScheduleViewModel
                    {
                        Lecturer = g.Key,
                        Schedules = g.ToList()
                    })
                    .ToList();

                PrepareViewBagForSchedule(lecturerId, searchName, semester, year, semesterPhase);

                if (!groupedSchedule.Any())
                {
                    ModelState.AddModelError("", "No teaching schedule found for the specified criteria.");
                }

                return View(groupedSchedule);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while searching for the schedule. Please try again.");
                PrepareViewBagForSchedule();
                return View();
            }
        }

        private void PrepareViewBagForSchedule(string? lecturerId = null, string? searchName = null, 
            int? semester = null, int? year = null, string? semesterPhase = null)
        {
            ViewBag.Lecturers = new SelectList(_context.Lecturers.OrderBy(l => l.FullName), "LecturerId", "FullName", lecturerId);
            ViewBag.Years = new SelectList(Enumerable.Range(DateTime.Now.Year - 1, 3), year);
            ViewBag.Semesters = new SelectList(new[] { 1, 2, 3 }, semester);
            ViewBag.SemesterPhases = new SelectList(new[] { "A", "B" }, semesterPhase);
            ViewBag.SearchName = searchName;
            ViewBag.LecturerId = lecturerId;
            ViewBag.Semester = semester;
            ViewBag.Year = year;
            ViewBag.SemesterPhase = semesterPhase;
        }

        private bool LecturerExists(string id)
        {
            return _context.Lecturers.Any(e => e.LecturerId == id);
        }
    }
} 