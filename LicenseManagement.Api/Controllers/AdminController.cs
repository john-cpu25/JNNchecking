using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LicenseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        /// <summary>
        /// Authenticates administrator credentials and returns a JWT admin token.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AdminLoginResponse>> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        {
            var response = await _adminService.AdminLoginAsync(request, ct);
            if (!response.Success)
            {
                return Unauthorized(response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Retrieves list of all licenses with optional email filter.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin,Support")]
        [HttpGet("licenses")]
        public async Task<ActionResult<List<LicenseSummaryDto>>> GetLicenses([FromQuery] string? search, CancellationToken ct)
        {
            var licenses = await _adminService.GetLicensesAsync(search, ct);
            return Ok(licenses);
        }

        /// <summary>
        /// Creates a new trial or annual license and generates a GUID activation code.
        /// Plaintext activation code is returned ONLY in this response.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("licenses")]
        public async Task<ActionResult<CreateLicenseResponse>> CreateLicense([FromBody] CreateLicenseRequest request, CancellationToken ct)
        {
            var response = await _adminService.CreateLicenseAsync(request, ct);
            return Ok(response);
        }

        /// <summary>
        /// Renews an active or expired license for 1 calendar year.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("licenses/{id:guid}/renew")]
        public async Task<ActionResult> RenewLicense([FromRoute] Guid id, CancellationToken ct)
        {
            bool success = await _adminService.RenewLicenseAsync(id, ct);
            if (!success) return NotFound(new { message = "License not found or could not be renewed." });

            return Ok(new { success = true, message = "License renewed successfully." });
        }

        public class RevokeRequest
        {
            public string Reason { get; set; } = "Administrative revocation";
        }

        /// <summary>
        /// Revokes a license, invalidating its activation codes and all registered devices.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("licenses/{id:guid}/revoke")]
        public async Task<ActionResult> RevokeLicense([FromRoute] Guid id, [FromBody] RevokeRequest request, CancellationToken ct)
        {
            bool success = await _adminService.RevokeLicenseAsync(id, request.Reason, ct);
            if (!success) return NotFound(new { message = "License not found." });

            return Ok(new { success = true, message = "License revoked successfully." });
        }

        /// <summary>
        /// Resets an active device association on a license, freeing up the activation slot.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("licenses/{id:guid}/reset-device/{deviceId:guid}")]
        public async Task<ActionResult> ResetDevice([FromRoute] Guid id, [FromRoute] Guid deviceId, CancellationToken ct)
        {
            bool success = await _adminService.ResetDeviceAsync(id, deviceId, ct);
            if (!success) return NotFound(new { message = "Device or license not found." });

            return Ok(new { success = true, message = "Device reset successfully." });
        }

        /// <summary>
        /// Blocks a user account from authenticating or validating licenses.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("users/{id:guid}/block")]
        public async Task<ActionResult> BlockUser([FromRoute] Guid id, CancellationToken ct)
        {
            bool success = await _adminService.SetUserBlockStatusAsync(id, true, ct);
            if (!success) return NotFound(new { message = "User not found." });

            return Ok(new { success = true, message = "User blocked successfully." });
        }

        /// <summary>
        /// Unblocks a user account.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost("users/{id:guid}/unblock")]
        public async Task<ActionResult> UnblockUser([FromRoute] Guid id, CancellationToken ct)
        {
            bool success = await _adminService.SetUserBlockStatusAsync(id, false, ct);
            if (!success) return NotFound(new { message = "User not found." });

            return Ok(new { success = true, message = "User unblocked successfully." });
        }

        /// <summary>
        /// Retrieves the recent license audit events log.
        /// </summary>
        [Authorize(Roles = "SuperAdmin,Admin,Support")]
        [HttpGet("license-events")]
        public async Task<ActionResult<List<LicenseEventDto>>> GetLicenseEvents([FromQuery] int limit = 100, CancellationToken ct = default)
        {
            var events = await _adminService.GetEventsAsync(Math.Clamp(limit, 1, 500), ct);
            return Ok(events);
        }
    }
}
