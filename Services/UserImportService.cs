using Microsoft.AspNetCore.Identity;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace TimetableManagementApp.Services;

public class UserImportService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UserImportService> _logger;

    public UserImportService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<UserImportService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<UserImportResult> ImportUsersFromExcel(Stream fileStream, string userType, string defaultPassword)
    {
        var result = new UserImportResult();
        result.Errors = new List<string>();
        
        try
        {
            // Mở workbook Excel
            var workbook = new XSSFWorkbook(fileStream);
            var sheet = workbook.GetSheetAt(0); // Lấy sheet đầu tiên

            var rowCount = sheet.LastRowNum;
            if (rowCount < 1)
            {
                result.Success = false;
                result.Message = "File Excel không có dữ liệu";
                return result;
            }

            // Xử lý từng dòng trong file Excel (bắt đầu từ dòng thứ 2, bỏ qua header)
            for (int i = 1; i <= rowCount; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    var email = GetCellValueAsString(row.GetCell(0));
                    var fullName = GetCellValueAsString(row.GetCell(1));
                    var password = GetCellValueAsString(row.GetCell(2));
                    
                    // Kiểm tra thông tin bắt buộc
                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
                    {
                        result.Errors.Add($"Dòng {i+1}: Email hoặc họ tên không được để trống");
                        result.ErrorCount++;
                        continue;
                    }

                    // Đảm bảo email hợp lệ
                    if (!email.Contains("@"))
                    {
                        result.Errors.Add($"Dòng {i+1}: Email {email} không hợp lệ");
                        result.ErrorCount++;
                        continue;
                    }
                    
                    // Sử dụng mật khẩu từ Excel nếu có, nếu không thì dùng mật khẩu mặc định
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        password = defaultPassword;
                    }
                    
                    // Kiểm tra mật khẩu có đủ mạnh không
                    if (!IsStrongPassword(password))
                    {
                        _logger.LogWarning($"Dòng {i+1}: Mật khẩu cho {email} không đủ mạnh, sẽ dùng mật khẩu mặc định");
                        password = !string.IsNullOrWhiteSpace(defaultPassword) && IsStrongPassword(defaultPassword) 
                            ? defaultPassword 
                            : GenerateStrongPassword();
                    }

                    // Kiểm tra email đã tồn tại chưa
                    var existingUser = await _userManager.FindByEmailAsync(email);
                    if (existingUser != null)
                    {
                        result.Errors.Add($"Dòng {i+1}: Email {email} đã tồn tại trong hệ thống");
                        result.ErrorCount++;
                        continue;
                    }

                    // Tạo tài khoản mới
                    var newUser = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        NormalizedUserName = email.ToUpper(),
                        NormalizedEmail = email.ToUpper(),
                        FullName = fullName,
                        Role = userType,
                        EmailConfirmed = true
                    };

                    var createResult = await _userManager.CreateAsync(newUser, password);
                    if (createResult.Succeeded)
                    {
                        // Gán role cho tài khoản
                        await _userManager.AddToRoleAsync(newUser, userType);
                        result.SuccessCount++;
                        
                        // Lưu lại thông tin về mật khẩu đã sử dụng
                        if (!result.ImportedUsers.ContainsKey(email))
                        {
                            result.ImportedUsers.Add(email, password);
                        }
                    }
                    else
                    {
                        var errorMessage = $"Dòng {i+1}: Không thể tạo tài khoản cho {email}. ";
                        foreach (var error in createResult.Errors)
                        {
                            errorMessage += error.Description + " ";
                            _logger.LogError($"Lỗi tạo tài khoản {email}: {error.Description}");
                        }
                        result.Errors.Add(errorMessage);
                        result.ErrorCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Dòng {i+1}: Lỗi xử lý - {ex.Message}");
                    result.ErrorCount++;
                    _logger.LogError($"Lỗi xử lý dữ liệu dòng {i+1}: {ex.Message}");
                }
            }

            result.Success = result.SuccessCount > 0;
            if (string.IsNullOrEmpty(result.Message)) {
                result.Message = $"Đã nhập {result.SuccessCount} tài khoản thành công, {result.ErrorCount} lỗi";
            } else {
                result.Message += $". Đã nhập {result.SuccessCount} tài khoản thành công, {result.ErrorCount} lỗi";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Lỗi xử lý file Excel: {ex.Message}";
            _logger.LogError($"Lỗi xử lý file Excel: {ex.Message}");
            return result;
        }
    }

    private string GetCellValueAsString(ICell cell)
    {
        if (cell == null) return string.Empty;

        switch (cell.CellType)
        {
            case CellType.Numeric:
                return cell.NumericCellValue.ToString();
            case CellType.String:
                return cell.StringCellValue.Trim();
            case CellType.Boolean:
                return cell.BooleanCellValue.ToString();
            case CellType.Formula:
                switch (cell.CachedFormulaResultType)
                {
                    case CellType.Numeric:
                        return cell.NumericCellValue.ToString();
                    case CellType.String:
                        return cell.StringCellValue.Trim();
                    default:
                        return string.Empty;
                }
            default:
                return string.Empty;
        }
    }

    private bool IsStrongPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;

        bool hasUpperCase = false;
        bool hasLowerCase = false;
        bool hasDigit = false;
        bool hasSpecialChar = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpperCase = true;
            else if (char.IsLower(c)) hasLowerCase = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecialChar = true;
        }

        return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
    }

    private string GenerateStrongPassword()
    {
        const string uppercaseChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercaseChars = "abcdefghijkmnopqrstuvwxyz";
        const string digitChars = "23456789";
        const string specialChars = "!@#$%^&*()_-+=";
        
        var random = new Random();
        var password = new StringBuilder();
        
        // Đảm bảo mật khẩu có ít nhất một ký tự từ mỗi nhóm
        password.Append(uppercaseChars[random.Next(uppercaseChars.Length)]);
        password.Append(lowercaseChars[random.Next(lowercaseChars.Length)]);
        password.Append(digitChars[random.Next(digitChars.Length)]);
        password.Append(specialChars[random.Next(specialChars.Length)]);
        
        // Thêm các ký tự ngẫu nhiên cho đến đủ độ dài
        const string allChars = uppercaseChars + lowercaseChars + digitChars + specialChars;
        for (int i = 4; i < 12; i++)
        {
            password.Append(allChars[random.Next(allChars.Length)]);
        }
        
        // Xáo trộn mật khẩu
        var passwordArray = password.ToString().ToCharArray();
        for (int i = 0; i < passwordArray.Length; i++)
        {
            int j = random.Next(i, passwordArray.Length);
            (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
        }
        
        return new string(passwordArray);
    }
} 