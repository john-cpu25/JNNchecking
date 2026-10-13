using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using LicenseManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseManagement.Admin.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IAdminService _adminService;

        public IndexModel(IAdminService adminService)
        {
            _adminService = adminService;
        }

        public List<LicenseSummaryDto> Licenses { get; set; } = new();
        public List<RegistrationRequestDto> PendingRequests { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public int TotalLicenses => Licenses.Count;
        public int ActiveLicenses => Licenses.Count(l => l.Status == LicenseStatus.Active);
        public int ExpiredLicenses => Licenses.Count(l => l.Status == LicenseStatus.Expired);
        public int ActiveDevicesCount => Licenses.Sum(l => l.ActiveDeviceCount);
        public int PendingRequestsCount => PendingRequests.Count;

        public string? SuccessMessage { get; set; }
        public string? GeneratedActivationCode { get; set; }
        public string? GeneratedEmail { get; set; }

        [BindProperty]
        public string NewEmail { get; set; } = string.Empty;

        [BindProperty]
        public LicenseType NewLicenseType { get; set; } = LicenseType.Annual;

        [BindProperty]
        public int NewMaxDevices { get; set; } = 1;

        public async Task OnGetAsync()
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            GeneratedActivationCode = TempData["GeneratedActivationCode"]?.ToString();
            GeneratedEmail = TempData["GeneratedEmail"]?.ToString();

            Licenses = await _adminService.GetLicensesAsync(Search);
            PendingRequests = await _adminService.GetPendingRegistrationRequestsAsync();
        }

        public async Task<IActionResult> OnPostCreateLicenseAsync()
        {
            if (string.IsNullOrWhiteSpace(NewEmail))
            {
                ModelState.AddModelError(string.Empty, "Email không được để trống.");
                Licenses = await _adminService.GetLicensesAsync(Search);
                PendingRequests = await _adminService.GetPendingRegistrationRequestsAsync();
                return Page();
            }

            var res = await _adminService.CreateLicenseAsync(new CreateLicenseRequest
            {
                Email = NewEmail.Trim(),
                LicenseType = NewLicenseType,
                MaxDevices = Math.Max(1, NewMaxDevices)
            });

            TempData["SuccessMessage"] = $"Đã tạo thành công bản quyền cho '{NewEmail}'.";
            TempData["GeneratedActivationCode"] = res.ActivationCode;
            TempData["GeneratedEmail"] = NewEmail;

            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnPostApproveRequestAsync(Guid requestId, int licenseType = 2)
        {
            var res = await _adminService.ApproveRegistrationRequestAsync(requestId, (LicenseType)licenseType);
            if (res != null)
            {
                TempData["SuccessMessage"] = "Đã phê duyệt yêu cầu đăng ký từ Revit thành công và sinh mã Key Code!";
                TempData["GeneratedActivationCode"] = res.ActivationCode;
            }
            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnPostRejectRequestAsync(Guid requestId)
        {
            bool ok = await _adminService.RejectRegistrationRequestAsync(requestId);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đã từ chối yêu cầu đăng ký.";
            }
            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnPostRenewAsync(Guid id)
        {
            bool ok = await _adminService.RenewLicenseAsync(id);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đã gia hạn thêm 1 năm thành công.";
            }
            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnPostRevokeAsync(Guid id, string reason = "Thu hồi bởi Quản trị viên")
        {
            bool ok = await _adminService.RevokeLicenseAsync(id, reason);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đã thu hồi bản quyền và vô hiệu hóa thiết bị liên kết.";
            }
            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnPostResetDeviceAsync(Guid licenseId, Guid deviceId)
        {
            bool ok = await _adminService.ResetDeviceAsync(licenseId, deviceId);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đã giải phóng thiết bị thành công. Người dùng có thể kích hoạt máy mới.";
            }
            return RedirectToPage(new { Search });
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            var list = await _adminService.GetLicensesAsync();

            var sb = new StringBuilder();
            sb.AppendLine("LicenseId,Email,LicenseType,Status,StartsAtUtc,ExpiresAtUtc,MaxDevices,ActiveDevices,CreatedAtUtc");

            foreach (var l in list)
            {
                sb.AppendLine($"\"{l.Id}\",\"{l.Email}\",\"{l.LicenseType}\",\"{l.Status}\",\"{l.StartsAtUtc:O}\",\"{l.ExpiresAtUtc:O}\",{l.MaxDevices},{l.ActiveDeviceCount},\"{l.CreatedAtUtc:O}\"");
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"jnntool_licenses_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }
    }
}
