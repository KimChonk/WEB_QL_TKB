using Microsoft.AspNetCore.Identity;
using TimetableManagementApp.Models;

namespace TimetableManagementApp.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Khởi tạo các role cho hệ thống
            string[] roleNames = { "Administrator", "Teacher", "Student", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Tạo tài khoản admin gốc
            var adminUser = await userManager.FindByEmailAsync("Admin@admin.com");
            if (adminUser == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "Admin@admin.com",
                    Email = "Admin@admin.com",
                    FullName = "System Administrator",
                    Role = "Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Administrator");
                }
            }
        }
    }
}