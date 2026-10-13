using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseManagement.Admin.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IAdminService _adminService;

        public LoginModel(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ Email và Mật khẩu.";
                return Page();
            }

            var authRes = await _adminService.AdminLoginAsync(new AdminLoginRequest
            {
                Email = Email.Trim(),
                Password = Password
            });

            if (!authRes.Success)
            {
                ErrorMessage = authRes.Message ?? "Thông tin đăng nhập không chính xác.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, authRes.Email),
                new(ClaimTypes.Email, authRes.Email),
                new(ClaimTypes.Role, authRes.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return RedirectToPage("/Index");
        }
    }
}
