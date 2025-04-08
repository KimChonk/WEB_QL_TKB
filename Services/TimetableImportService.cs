using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using TimetableManagementApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TimetableManagementApp.Services;

public class TimetableImportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TimetableImportService> _logger;

    public TimetableImportService(ApplicationDbContext context, ILogger<TimetableImportService> logger)
    {
        _context = context;
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<(bool Success, string Message, List<Timetable> ImportedTimetables)> ImportFromExcel(Stream stream)
    {
        var timetables = new List<Timetable>();
        var message = "";
        bool success = true;
        
        try
        {
            _logger.LogInformation("Starting Excel import process");
            await ProcessExcelFile(stream, timetables);
            
            if (timetables.Count > 0)
            {
                await _context.Timetables.AddRangeAsync(timetables);
                await _context.SaveChangesAsync();
                message = $"Successfully imported {timetables.Count} timetable records.";
                _logger.LogInformation(message);
            }
            else
            {
                message = "No valid records found to import.";
                _logger.LogWarning(message);
                success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing timetable from Excel");
            message = $"Error importing timetable: {ex.Message}";
            if (ex.InnerException != null)
            {
                message += $" Detail: {ex.InnerException.Message}";
            }
            success = false;
        }
        
        return (success, message, timetables);
    }

    private async Task ProcessExcelFile(Stream stream, List<Timetable> timetables)
    {
        using (var package = new ExcelPackage(stream))
        {
            var worksheet = package.Workbook.Worksheets[0]; // Get the first worksheet
            var rowCount = worksheet.Dimension?.Rows ?? 0;

            // Check headers to help user ensure correct columns
            if (rowCount > 0)
            {
                // Check header row (row 1)
                Console.WriteLine("Checking Excel headers:");
                for (int col = 1; col <= 15; col++)
                {
                    string headerValue = worksheet.Cells[1, col].Value?.ToString() ?? "";
                    Console.WriteLine($"Column {GetExcelColumnName(col)} ({col}): '{headerValue}'");
                }
                
                // Specifically check type and semester phase headers
                var typeHeader = worksheet.Cells[1, 13].Value?.ToString() ?? "";
                var phaseHeader = worksheet.Cells[1, 14].Value?.ToString() ?? "";
                
                Console.WriteLine($"Type column (M): '{typeHeader}'");
                Console.WriteLine($"SemesterPhase column (N): '{phaseHeader}'");
                
                if (!typeHeader.Contains("Type") && !typeHeader.Contains("Loại"))
                {
                    Console.WriteLine("WARNING: Column M may not contain the Type data - check Excel format");
                }
                
                if (!phaseHeader.Contains("Semester") && !phaseHeader.Contains("Học kỳ"))
                {
                    Console.WriteLine("WARNING: Column N may not contain the SemesterPhase data - check Excel format");
                }
            }

            // Skip header row, start from row 2
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    // Kiểm tra xem hàng có dữ liệu không
                    if (worksheet.Cells[row, 1].Value == null && worksheet.Cells[row, 2].Value == null)
                        continue;

                    // Debug: Print values from Excel for troubleshooting
                    Console.WriteLine($"Row {row} raw values:");
                    Console.WriteLine($"- Column M (Type): '{worksheet.Cells[row, 13].Value}'");
                    Console.WriteLine($"- Column N (SemesterPhase): '{worksheet.Cells[row, 14].Value}'");

                    // Convert Thứ trong tuần to integer DayOfWeek
                    int? dayOfWeek = null;
                    var dayString = worksheet.Cells[row, 6].Value?.ToString();
                    if (!string.IsNullOrEmpty(dayString))
                    {
                        if (dayString.Contains("2"))
                            dayOfWeek = 2; // Thứ 2
                        else if (dayString.Contains("3"))
                            dayOfWeek = 3; // Thứ 3
                        else if (dayString.Contains("4"))
                            dayOfWeek = 4; // Thứ 4
                        else if (dayString.Contains("5"))
                            dayOfWeek = 5; // Thứ 5
                        else if (dayString.Contains("6"))
                            dayOfWeek = 6; // Thứ 6
                        else if (dayString.Contains("7"))
                            dayOfWeek = 7; // Thứ 7
                        else if (dayString.ToLower().Contains("chủ nhật"))
                            dayOfWeek = 1; // Chủ nhật
                    }

                    // Parse đúng định dạng ngày trong Excel
                    DateTime? dateStart = null;
                    if (worksheet.Cells[row, 9].Value != null)
                    {
                        if (worksheet.Cells[row, 9].Value is DateTime dateTimeStart)
                        {
                            dateStart = dateTimeStart;
                        }
                        else if (DateTime.TryParse(worksheet.Cells[row, 9].Value.ToString(), out DateTime parsedDateStart))
                        {
                            dateStart = parsedDateStart;
                        }
                    }

                    DateTime? dateEnd = null;
                    if (worksheet.Cells[row, 10].Value != null)
                    {
                        if (worksheet.Cells[row, 10].Value is DateTime dateTimeEnd)
                        {
                            dateEnd = dateTimeEnd;
                        }
                        else if (DateTime.TryParse(worksheet.Cells[row, 10].Value.ToString(), out DateTime parsedDateEnd))
                        {
                            dateEnd = parsedDateEnd;
                        }
                    }

                    // Add validation for date range
                    if (dateStart.HasValue && dateEnd.HasValue)
                    {
                        var validStartDate = new DateTime(2023, 9, 1);
                        var validEndDate = new DateTime(2023, 12, 31);

                        if (dateStart.Value < validStartDate || dateEnd.Value > validEndDate)
                        {
                            _logger.LogWarning($"Skipping row {row} due to invalid date range: {dateStart.Value} - {dateEnd.Value}");
                            continue;
                        }
                    }

                    // Ensure string values don't exceed column limits
                    var courseCode = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                    if (courseCode.Length > 20) courseCode = courseCode.Substring(0, 20);
                    
                    var lecturerId = worksheet.Cells[row, 3].Value?.ToString() ?? "N/A";
                    if (lecturerId.Length > 10) lecturerId = lecturerId.Substring(0, 10);
                    
                    var roomId = worksheet.Cells[row, 5].Value?.ToString() ?? "DefaultRoom";
                    if (roomId.Length > 20) roomId = roomId.Substring(0, 20);
                    
                    // For type (column M = 13), carefully handle the value
                    string type;
                    var typeRawValue = worksheet.Cells[row, 13].Value;
                    // Ensure typeRawValue is not null before processing
                    if (typeRawValue != null)
                    {
                        type = typeRawValue.ToString().Trim().ToUpper();
                        if (type.Length > 5) 
                            type = type.Substring(0, 5);
                        if (type.Contains("LT") || type.Contains("LÝ") || type.Contains("LY"))
                            type = "LT";
                        else if (type.Contains("TH") || type.Contains("THỰC") || type.Contains("THUC"))
                            type = "TH";
                        else if (string.IsNullOrWhiteSpace(type))
                            type = "N/A";
                    }
                    else
                    {
                        type = "N/A";
                    }

                    // Get Semester Phase (column N = 14)
                    string semesterPhase = "1"; // Default to phase 1
                    var phaseRawValue = worksheet.Cells[row, 14].Value;
                    // Ensure phaseRawValue is not null before processing
                    if (phaseRawValue != null)
                    {
                        var rawValue = phaseRawValue.ToString().Trim();
                        if (!string.IsNullOrEmpty(rawValue))
                        {
                            foreach (char c in rawValue)
                            {
                                if (char.IsDigit(c))
                                {
                                    semesterPhase = c.ToString();
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        semesterPhase = "1"; // Default value
                    }

                    // Parse GroupID from Excel with additional logging
                    int? groupId = null;
                    var groupIdValue = worksheet.Cells[row, 1].Value;
                    if (groupIdValue != null)
                    {
                        Console.WriteLine($"Row {row}: GroupID raw value: '{groupIdValue}'");
                        if (int.TryParse(groupIdValue.ToString(), out int parsedGroupId))
                        {
                            groupId = parsedGroupId;
                            Console.WriteLine($"Row {row}: GroupID parsed value: {groupId}");
                        }
                        else
                        {
                            Console.WriteLine($"Row {row}: Could not parse GroupID");
                        }
                    }

                    // Parse ClassID and set ClassIDInt as well
                    int classId = 0; // Default to 0 if not specified
                    if (int.TryParse(worksheet.Cells[row, 4].Value?.ToString(), out int parsedClassId))
                    {
                        classId = parsedClassId;
                        Console.WriteLine($"Row {row}: ClassID parsed value: {classId}");
                    }
                    else
                    {
                        // If ClassID is missing or invalid, we'll use a default class or create one later
                        Console.WriteLine($"Row {row}: ClassID not specified or invalid, will use default");
                    }

                    // Xử lý ClassIDInt như chuỗi
                    // Handle nullable values for classNameFromExcel and classIDIntFromExcel
                    string classNameFromExcel = worksheet.Cells[row, 3].Value?.ToString()?.Trim() ?? "Default Class";
                    string classIDIntFromExcel = worksheet.Cells[row, 4].Value?.ToString()?.Trim() ?? "0";

                    var timetable = new Timetable
                    {
                        CourseCode = string.IsNullOrEmpty(courseCode) ? "DEFAULT001" : courseCode,
                        LecturerId = lecturerId, // Set LecturerId to the value from Excel
                        LecturerId1 = lecturerId, // Set LecturerId1 to the same value as LecturerId
                        ClassID = classId, // This will be either the parsed ID or 0, we'll update it in CreateOrValidateForeignKeys
                        ClassIDInt = classIDIntFromExcel,
                        RoomID = string.IsNullOrEmpty(roomId) ? "DefaultRoom" : roomId,
                        DayOfWeek = dayOfWeek,
                        StartPeriod = int.TryParse(worksheet.Cells[row, 7].Value?.ToString(), out int startPeriod) ? startPeriod : null,
                        NumPeriods = int.TryParse(worksheet.Cells[row, 8].Value?.ToString(), out int numPeriods) ? numPeriods : null,
                        DateStart = dateStart,
                        DateEnd = dateEnd,
                        GroupID = groupId, // Using the parsed groupId with logging
                        Type = type,
                        SemesterPhase = semesterPhase,
                        Notes = "Imported from Excel", // Set default Notes value
                        IsActive = true // Set default IsActive value
                    };

                    // Create or validate foreign key relationships
                    try
                    {
                        // Ensure non-null argument for CreateOrValidateForeignKeys
                        await CreateOrValidateForeignKeys(timetable, row, classNameFromExcel ?? "Default Class");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error in row {row}: {ex.Message} {(ex.InnerException != null ? "Inner exception: " + ex.InnerException.Message : "")}", ex);
                    }

                    timetables.Add(timetable);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error processing row {row}: {ex.Message}", ex);
                }
            }
        }
    }

    public async Task CreateOrValidateForeignKeys(Timetable timetable, int row, string classNameFromExcel = null)
    {
        // Skip validation if CourseCode is null or empty
        if (string.IsNullOrEmpty(timetable.CourseCode))
        {
            throw new Exception($"CourseCode is required but was empty");
        }

        // Check if the course exists, if not create it
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseCode == timetable.CourseCode);
        if (course == null)
        {
            // Ensure course values don't exceed column limits
            string courseName = $"Khóa học {timetable.CourseCode}";
            if (courseName.Length > 100) courseName = courseName.Substring(0, 100);
            
            string department = "Chưa cập nhật";
            if (department.Length > 50) department = department.Substring(0, 50);
            
            course = new Course
            {
                CourseCode = timetable.CourseCode,
                CourseName = courseName,
                Credits = 3,
                Department = department,
                Description = "Mô tả khóa học sẽ được cập nhật sau."
            };
            _context.Courses.Add(course);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create Course: {ex.Message}", ex);
            }
        }

        // Set CourseCode for the timetable
        timetable.CourseCode = course.CourseCode;
        timetable.Course = course;

        // Check if the class exists, if not create it
        // If ClassID is 0, we need to create or find a default class
        var classObj = timetable.ClassID == 0 ? 
            null : 
            await _context.Classes.FirstOrDefaultAsync(c => c.ClassID == timetable.ClassID);
            
        if (classObj == null)
        {
            // First try to find a default class if one exists
            var defaultClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassName == "Default Class");
            if (defaultClass != null)
            {
                classObj = defaultClass;
                timetable.ClassID = defaultClass.ClassID;
                timetable.ClassIDInt = defaultClass.ClassID.ToString(); // Convert to string for ClassIDInt
            }
            else
            {
                // Get class name from Excel or generate a default name
                string className = "Default Class";
                if (!string.IsNullOrEmpty(classNameFromExcel))
                {
                    className = classNameFromExcel.Trim();
                }
                else if (timetable.ClassID != 0)
                {
                    className = $"Lớp {timetable.ClassID}";
                }
                
                // Check if a class with this name already exists
                var existingClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassName == className);
                
                if (existingClass != null)
                {
                    // Use the existing class if one with matching name is found
                    classObj = existingClass;
                    timetable.ClassID = existingClass.ClassID;
                    timetable.ClassIDInt = existingClass.ClassID.ToString(); // Convert to string for ClassIDInt
                }
                else
                {
                    // Create a new class and let the database generate the ID
                    classObj = new Class
                    {
                        ClassName = className,
                        ClassSize = 40 // Default class size
                    };
                    _context.Classes.Add(classObj);

                    try {
                        await _context.SaveChangesAsync();
                        
                        // Get the newly created class with its generated ID
                        classObj = await _context.Classes
                            .Where(c => c.ClassName == className)
                            .FirstOrDefaultAsync();
                            
                        if (classObj != null) {
                            // Update the timetable with the newly generated ClassID
                            timetable.ClassID = classObj.ClassID;
                            timetable.ClassIDInt = classObj.ClassID.ToString(); // Convert to string for ClassIDInt
                        }
                        else
                        {
                            throw new Exception("Failed to retrieve newly created Class");
                        }
                    } catch (Exception ex) {
                        throw new Exception($"Failed to create Class: {ex.Message}", ex);
                    }
                }
            }
        }
        timetable.Class = classObj;

        // Check if the room exists, if not create it
        if (string.IsNullOrEmpty(timetable.RoomID))
        {
            timetable.RoomID = "DefaultRoom";
        }
        
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomID == timetable.RoomID);
        if (room == null)
        {
            string roomName = $"Phòng {timetable.RoomID}";
            if (roomName.Length > 50) roomName = roomName.Substring(0, 50);
            
            string location = "Chưa cập nhật";
            if (location.Length > 100) location = location.Substring(0, 100);
            
            room = new Room
            {
                RoomID = timetable.RoomID,
                RoomName = roomName,
                Capacity = 40,
                Location = location
            };
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();
        }
        timetable.Room = room;

        // Handle lecturer and user relationships
        string lecturerId = timetable.LecturerId ?? "DEFAULT";
        var lecturer = await _context.Lecturers
            .Where(l => l.LecturerId == lecturerId)
            .FirstOrDefaultAsync();
        
        if (lecturer == null)
        {
            // Ensure names don't exceed max length
            string fullName = "Default Lecturer";
            string name = "Default";
            string email = "default@university.com";
            string department = "Default Department";
            
            if (!string.IsNullOrEmpty(timetable.LecturerId))
            {
                fullName = timetable.LecturerId.Length > 100 ? timetable.LecturerId.Substring(0, 100) : timetable.LecturerId;
                name = timetable.LecturerId.Length > 50 ? timetable.LecturerId.Substring(0, 50) : timetable.LecturerId;
                email = $"{timetable.LecturerId.ToLower()}@university.com";
                if (email.Length > 100) email = email.Substring(0, 100);
            }
            
            lecturer = new Lecturer
            {
                LecturerId = lecturerId,
                FullName = fullName,
                Name = name,
                Email = email,
                Phone = "N/A",
                Department = department
            };
            
            _context.Lecturers.Add(lecturer);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create Lecturer: {ex.Message}", ex);
            }
        }
        
        // Link timetable with lecturer
        timetable.Lecturer = lecturer;
        timetable.LecturerId = lecturer.LecturerId;
        timetable.LecturerId1 = lecturer.LecturerId; // Make sure LecturerId1 is updated too
        
        // Find or use an existing user to link with the timetable
        var applicationUser = await _context.Users
            .Where(u => u.Role == "Lecturer" && u.Email == lecturer.Email)
            .FirstOrDefaultAsync();
            
        if (applicationUser == null)
        {
            // If no matching lecturer user found, try to get an admin user
            applicationUser = await _context.Users
                .Where(u => u.Role == "Administrator")
                .FirstOrDefaultAsync();
                
            if (applicationUser == null)
            {
                // If no admin user, get any user
                applicationUser = await _context.Users.FirstOrDefaultAsync();
                
                if (applicationUser == null)
                {
                    // No users found at all - create a warning but don't stop the import
                    Console.WriteLine("Warning: No users found in AspNetUsers to link with timetable");
                    timetable.ApplicationUserId = null;
                }
                else
                {
                    timetable.ApplicationUserId = applicationUser.Id;
                    timetable.ApplicationUser = applicationUser;
                }
            }
            else
            {
                timetable.ApplicationUserId = applicationUser.Id;
                timetable.ApplicationUser = applicationUser;
            }
        }
        else
        {
            timetable.ApplicationUserId = applicationUser.Id;
            timetable.ApplicationUser = applicationUser;
        }
        
        // Initialize notes and active status
        timetable.Notes = "Imported from Excel";
        timetable.IsActive = true;
        
        // Automatically create 20 students for this course
        if (!string.IsNullOrEmpty(timetable.CourseCode))
        {
            // Check if we already have students enrolled in this course
            var enrollmentCount = await _context.StudentCourses
                .Where(sc => sc.CourseCode == timetable.CourseCode)
                .CountAsync();

            if (enrollmentCount == 0)
            {
                // Get available students that are not already enrolled in this course
                var students = await _context.Users
                    .Where(u => u.Role == "Student")
                    .Take(20)
                    .ToListAsync();

                if (students.Count > 0)
                {
                    foreach (var student in students)
                    {
                        var studentCourse = new StudentCourse
                        {
                            StudentId = student.Id,
                            CourseCode = timetable.CourseCode,
                            EnrollmentDate = DateTime.Now
                        };

                        _context.StudentCourses.Add(studentCourse);
                    }
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Đảm bảo ClassIDInt luôn được lưu dưới dạng chuỗi
        if (string.IsNullOrEmpty(timetable.ClassIDInt))
        {
            // Nếu ClassIDInt rỗng, có thể gán giá trị mặc định hoặc dựa trên ClassID
            timetable.ClassIDInt = timetable.ClassID.ToString();
        }
    }

    // Helper method to convert column number to Excel column letter (A, B, C, ..., AA, AB, etc.)
    private string GetExcelColumnName(int columnNumber)
    {
        string columnName = "";
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }
        return columnName;
    }
}