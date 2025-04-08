using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimetableManagementApp.Models;
using TimetableManagementApp.ViewModels;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace TimetableManagementApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        // Create a new RegisterViewModel with initialized AvailableRoles
        var model = new RegisterViewModel();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Check if someone is trying to register as an Administrator
            if (model.Role == "Administrator")
            {
                // Check if any admins already exist
                var adminExists = await _userManager.GetUsersInRoleAsync("Administrator");
                if (adminExists.Count > 0)
                {
                    // Only allow admin registration if there are no admin users yet
                    // For security reasons, subsequent admin users should be created by existing admins
                    ModelState.AddModelError(string.Empty, "Administrator accounts can only be created by existing administrators.");
                    return View(model);
                }
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Assign the role from the form
                await _userManager.AddToRoleAsync(user, model.Role);
                
                await _signInManager.SignInAsync(user, isPersistent: false);
                _logger.LogInformation($"User {model.Email} registered successfully with role {model.Role}");
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                
                // Lấy thông tin người dùng
                var user = await _userManager.FindByEmailAsync(model.Email);
                
                // Kiểm tra vai trò của người dùng và chuyển hướng tương ứng
                if (user != null && await _userManager.IsInRoleAsync(user, "Student"))
                {
                    return RedirectToAction("Index", "Student");
                }
                else if (user != null && await _userManager.IsInRoleAsync(user, "Teacher"))
                {
                    return RedirectToAction("Index", "Teacher");
                }
                else if (user != null && await _userManager.IsInRoleAsync(user, "Administrator"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                
                return RedirectToLocal(returnUrl);
            }
            
            if (result.RequiresTwoFactor)
            {
                return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, model.RememberMe });
            }
            
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }
        }
        
        // If we got this far, something failed, redisplay form
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult LoginWith2fa(bool rememberMe, string returnUrl = null)
    {
        // Phương thức này sẽ được gọi nếu xác thực 2 yếu tố được yêu cầu
        // Trong ứng dụng mẫu này, chúng ta sẽ trả về trang không tìm thấy
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Lockout()
    {
        // Phương thức này sẽ được gọi khi tài khoản bị khóa
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        else
        {
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }
} 