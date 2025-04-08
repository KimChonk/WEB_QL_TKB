using System;
using System.IO;
using System.Reflection;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimetableManagementApp.Models;
using TimetableManagementApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình URL và cổng 5005 cụ thể
builder.WebHost.UseUrls("http://localhost:5005");

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });
builder.Services.AddRazorPages();

// Tăng giới hạn kích thước header để tránh lỗi HTTP 431
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 65536; // Tăng giới hạn lên 64KB
    options.Limits.MaxRequestLineSize = 16384; // Tăng giới hạn dòng request
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // Tăng giới hạn body size lên 100MB
});

// Tăng giới hạn kích thước form
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

// Configure SQL Server database with exception handling
try
{
    Console.WriteLine("Attempting to configure SQL Server connection...");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
    });
    Console.WriteLine("SQL Server configuration completed.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error configuring SQL Server: {ex.Message}");
    throw;
}

// Register Services
builder.Services.AddScoped<TimetableImportService>();
builder.Services.AddScoped<UserImportService>();
builder.Services.AddScoped<StudentCourseImportService>();

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Thiết lập chính sách mật khẩu dễ dàng hơn cho mục đích demo
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 3;

    // Không yêu cầu xác nhận email
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;

    // Tắt khóa tài khoản khi đăng nhập thất bại
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministratorRole",
        policy => policy.RequireRole("Administrator"));
    options.AddPolicy("RequireTeacherRole",
        policy => policy.RequireRole("Teacher", "Administrator"));
    options.AddPolicy("RequireStudentRole",
        policy => policy.RequireRole("Student", "Administrator"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Thêm middleware kiểm tra vai trò và chuyển hướng vào trước MapControllerRoute
app.Use(async (context, next) => 
{
    if (context.Request.Path == "/" && context.User.Identity.IsAuthenticated)
    {
        if (context.User.IsInRole("Student"))
        {
            context.Response.Redirect("/Student/Index");
            return;
        }
        else if (context.User.IsInRole("Teacher")) 
        {
            context.Response.Redirect("/Teacher/Index");
            return;
        }
        else if (context.User.IsInRole("Administrator"))
        {
            context.Response.Redirect("/Admin/Index");
            return;
        }
    }
    
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Create default roles and admin user
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    
    // Gọi DbInitializer để khởi tạo các vai trò và tài khoản admin gốc
    try
    {
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate(); // Đảm bảo database được cập nhật
        
        // Khởi tạo các vai trò và tài khoản admin
        await TimetableManagementApp.Data.DbInitializer.Initialize(serviceProvider);
        Console.WriteLine("Database initialized with roles and admin account (Admin@admin.com)");
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
        Console.WriteLine($"Error initializing database: {ex.Message}");
    }
    
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

    // Check for lecturer account with email lecturer1001@university.com
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var lecturer1001 = await userManager.FindByEmailAsync("lecturer1001@university.com");
    
    if (lecturer1001 != null)
    {
        Console.WriteLine($"Found lecturer account: {lecturer1001.Email}, ID: {lecturer1001.Id}");
        
        // Check if there's a lecturer with this email in the Lecturers table - BUT avoid accessing UserId
        try
        {
            var lecturerRecord = await context.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM Lecturers WHERE Email = {0}", "lecturer1001@university.com");
            
            if (lecturerRecord == 0) // No rows returned
            {
                Console.WriteLine("No matching lecturer record found in Lecturers table. Creating one...");
                
                try
                {
                    // Check if ID 1001 already exists
                    var existingSql = "SELECT COUNT(1) FROM Lecturers WHERE LecturerId = '1001'";
                    var existingCount = await context.Database.ExecuteSqlRawAsync(existingSql);
                    
                    if (existingCount > 0)
                    {
                        // If exists, we'll update it instead
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE Lecturers SET Email = {0}, FullName = {1}, Name = {2}, Phone = {3}, Department = {4} WHERE LecturerID = '1001'",
                            "lecturer1001@university.com",
                            lecturer1001.FullName ?? "Giảng viên 1001",
                            "GV 1001",
                            "N/A",
                            "Chưa cập nhật");
                        
                        Console.WriteLine("Updated existing lecturer record with ID: 1001");
                    }
                    else
                    {
                        // Create a new lecturer with raw SQL
                        await context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO Lecturers (LecturerID, FullName, Name, Email, Phone, Department) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                            "1001",
                            lecturer1001.FullName ?? "Giảng viên 1001",
                            "GV 1001",
                            "lecturer1001@university.com",
                            "N/A",
                            "Chưa cập nhật");
                        
                        Console.WriteLine("Created new lecturer record with ID: 1001");
                    }
                    
                    // Try to set UserId if column exists
                    await context.Database.ExecuteSqlRawAsync(
                        "IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'UserId' AND object_id = OBJECT_ID('Lecturers')) " +
                        "UPDATE Lecturers SET UserId = {0} WHERE LecturerID = '1001'",
                        lecturer1001.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating/updating lecturer: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Found lecturer record with Email: lecturer1001@university.com");
                
                // Try to set UserId if column exists
                try
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'UserId' AND object_id = OBJECT_ID('Lecturers')) " +
                        "UPDATE Lecturers SET UserId = {0} WHERE Email = 'lecturer1001@university.com'",
                        lecturer1001.Id);
                }
                catch
                {
                    // Ignore errors here, we'll fix it with migrations
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for lecturer: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    // Mark the migration as applied if it's not already in the database
    try
    {
        if (context.Database.GetPendingMigrations().Any(m => m == "20250330083727_Initial"))
        {
            // Create __EFMigrationsHistory table if it doesn't exist
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                BEGIN
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
                END;
            ");

            // Insert the migration record
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250330083727_Initial')
                BEGIN
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'20250330083727_Initial', N'9.0.3');
                END
            ");

            Console.WriteLine("Migration 20250330083727_Initial marked as applied.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error marking migration as applied: {ex.Message}");
    }
}

if (args.Length > 0 && args[0] == "describe-table")
{
    if (args.Length > 1)
    {
        var tableName = args[1];
        var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        try
        {
            // Get database connection
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();
                
            using var command = connection.CreateCommand();
            
            // Query to get column information
            command.CommandText = $@"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.max_length AS MaxLength,
                    c.is_nullable AS IsNullable
                FROM 
                    sys.columns c
                INNER JOIN 
                    sys.types t ON c.user_type_id = t.user_type_id
                INNER JOIN 
                    sys.tables tb ON c.object_id = tb.object_id
                WHERE 
                    tb.name = '{tableName}'
            ";
            
            using var reader = command.ExecuteReader();
            Console.WriteLine($"Schema for table {tableName}:");
            Console.WriteLine("------------------------------------");
            
            while (reader.Read())
            {
                var columnName = reader["ColumnName"].ToString();
                var dataType = reader["DataType"].ToString();
                var maxLength = reader["MaxLength"].ToString();
                var isNullable = Convert.ToBoolean(reader["IsNullable"]) ? "NULL" : "NOT NULL";
                
                Console.WriteLine($"{columnName} - {dataType}({maxLength}) {isNullable}");
            }
            
            connection.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking database schema: {ex.Message}");
        }
    }
}

if (args.Length > 0 && args[0] == "execute-script")
{
    if (args.Length > 1)
    {
        var scriptPath = args[1];
        if (File.Exists(scriptPath))
        {
            var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            try
            {
                // Get the connection and load the SQL script
                var connection = dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();
                    
                var scriptContent = File.ReadAllText(scriptPath);
                
                // Split the script by GO statements if there are any
                var statements = scriptContent.Split(new[] { "GO", "Go", "go" }, 
                    StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var statement in statements)
                {
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = statement;
                        command.CommandTimeout = 300; // 5 minutes timeout
                        
                        try
                        {
                            var affected = command.ExecuteNonQuery();
                            Console.WriteLine($"Executed statement. Rows affected: {affected}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing statement: {ex.Message}");
                            Console.WriteLine($"Statement: {statement}");
                        }
                    }
                }
                
                connection.Close();
                Console.WriteLine("Script execution completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing script: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"Script file not found: {scriptPath}");
        }
    }
}

app.Run();
