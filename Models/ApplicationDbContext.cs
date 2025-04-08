using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TimetableManagementApp.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Class> Classes { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Lecturer> Lecturers { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Timetable> Timetables { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<CourseEnrollment> CourseEnrollments { get; set; }
    public DbSet<StudentCourse> StudentCourses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassID).HasName("PK__Classes__CB1927A0EDD320BF");
            entity.Property(e => e.ClassID);
            entity.Property(e => e.ClassName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseCode);
            entity.Property(e => e.CourseCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CourseName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Lecturer>(entity =>
        {
            entity.HasKey(e => e.LecturerId).HasName("PK__Lecturer__5A78B91DE69E3492");
            entity.Property(e => e.LecturerId)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("LecturerID");
            entity.Property(e => e.Department)
                .HasMaxLength(50)
                .IsUnicode(true);
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FullName)
                .HasMaxLength(50)
                .IsUnicode(true);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
                
            // Configure relationship with ApplicationUser
            entity.HasOne(d => d.User)
                .WithOne()
                .HasForeignKey<Lecturer>(d => d.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomID).HasName("PK__Rooms__32863919FAB17ABA");
            entity.Property(e => e.RoomID)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Location)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Timetable>(entity =>
        {
            entity.HasKey(e => e.TimetableID);
            entity.Property(e => e.CourseCode).HasMaxLength(20);
            entity.Property(e => e.SemesterPhase).HasMaxLength(1);
            entity.Property(e => e.RoomID).HasMaxLength(20);
            entity.Property(e => e.LecturerId).HasMaxLength(10);
            entity.Property(e => e.Type).HasMaxLength(5);

            entity.HasOne(d => d.Class)
                .WithMany(p => p.Timetables)
                .HasForeignKey(d => d.ClassID);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.Timetables)
                .HasForeignKey(d => d.CourseCode);

            entity.HasOne(d => d.Lecturer)
                .WithMany(p => p.Timetables)
                .HasForeignKey(d => d.LecturerId);
                
            entity.HasOne(d => d.ApplicationUser)
                .WithMany(p => p.Timetables)
                .HasForeignKey(d => d.ApplicationUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Room)
                .WithMany(p => p.Timetables)
                .HasForeignKey(d => d.RoomID);
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId);
            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.TimetableID).IsRequired();
            entity.Property(e => e.AttendanceDate).IsRequired();
            entity.Property(e => e.Note).IsRequired();

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Timetable)
                .WithMany(p => p.Attendances)
                .HasForeignKey(d => d.TimetableID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseEnrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId);
            entity.Property(e => e.CourseCode).IsRequired();
            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.EnrollmentDate).IsRequired();

            entity.HasOne(d => d.Course)
                .WithMany(p => p.CourseEnrollments)
                .HasForeignKey(d => d.CourseCode)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany(p => p.CourseEnrollments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CourseCode, e.StudentId }).IsUnique();
        });

        // Configure StudentCourse entity
        modelBuilder.Entity<StudentCourse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EnrollmentDate).IsRequired();

            entity.HasOne(d => d.Student)
                .WithMany(p => p.EnrolledCourses)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.StudentCourses)
                .HasForeignKey(d => d.CourseCode)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure ApplicationUser entity
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasMany(u => u.Timetables)
                .WithOne(t => t.ApplicationUser)
                .HasForeignKey(t => t.ApplicationUserId)
                .IsRequired(false);
                
            entity.HasMany(u => u.EnrolledCourses)
                .WithOne(sc => sc.Student)
                .HasForeignKey(sc => sc.StudentId)
                .IsRequired();
                
            entity.HasMany(u => u.CourseEnrollments)
                .WithOne(ce => ce.Student)
                .HasForeignKey(ce => ce.StudentId)
                .IsRequired();
        });
    }
}