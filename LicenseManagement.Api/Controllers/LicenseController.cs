using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LicenseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/license")]
    public class LicenseController : ControllerBase
    {
        private readonly ILicenseService _licenseService;

        public LicenseController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        public class DirectValidateRequest
        {
            public Guid LicenseId { get; set; }
            public string DeviceIdentifierHash { get; set; } = string.Empty;
        }

        /// <summary>
        /// Validates license status and active device heartbeat.
        /// Can be called with JWT Bearer or with direct licenseId + deviceHash.
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<ValidateResponse>> Validate([FromBody] DirectValidateRequest request, CancellationToken ct)
        {
            Guid licenseId = request.LicenseId;
            string deviceHash = request.DeviceIdentifierHash;

            // If JWT token is present, fallback/override with token claims
            if (User.Identity?.IsAuthenticated == true)
            {
                var licenseClaim = User.FindFirst("license_id")?.Value;
                var deviceClaim = User.FindFirst("device_hash")?.Value;

                if (Guid.TryParse(licenseClaim, out var claimLicenseId))
                {
                    licenseId = claimLicenseId;
                }
                if (!string.IsNullOrWhiteSpace(deviceClaim))
                {
                    deviceHash = deviceClaim;
                }
            }

            if (licenseId == Guid.Empty || string.IsNullOrWhiteSpace(deviceHash))
            {
                return BadRequest(new ValidateResponse { IsValid = false, Message = "License ID and device identifier are required." });
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = await _licenseService.ValidateAsync(licenseId, deviceHash, ipAddress, ct);

            return Ok(response);
        }

        /// <summary>
        /// Retrieves the current license status for the authenticated client.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ValidateResponse>> GetMyLicense(CancellationToken ct)
        {
            var licenseClaim = User.FindFirst("license_id")?.Value;
            var deviceClaim = User.FindFirst("device_hash")?.Value;

            if (!Guid.TryParse(licenseClaim, out var licenseId) || string.IsNullOrWhiteSpace(deviceClaim))
            {
                return Unauthorized(new ValidateResponse { IsValid = false, Message = "Invalid license token claims." });
            }

            var response = await _licenseService.GetLicenseStatusAsync(licenseId, deviceClaim, ct);
            return Ok(response);
        }

        public class DirectDeactivateRequest
        {
            public Guid LicenseId { get; set; }
            public string DeviceIdentifierHash { get; set; } = string.Empty;
        }

        /// <summary>
        /// Deactivates a device, freeing its slot for another machine.
        /// </summary>
        [HttpPost("deactivate")]
        public async Task<ActionResult<DeactivateResponse>> Deactivate([FromBody] DirectDeactivateRequest request, CancellationToken ct)
        {
            Guid licenseId = request.LicenseId;
            string deviceHash = request.DeviceIdentifierHash;

            if (User.Identity?.IsAuthenticated == true)
            {
                var licenseClaim = User.FindFirst("license_id")?.Value;
                var deviceClaim = User.FindFirst("device_hash")?.Value;

                if (Guid.TryParse(licenseClaim, out var claimLicenseId))
                {
                    licenseId = claimLicenseId;
                }
                if (!string.IsNullOrWhiteSpace(deviceClaim))
                {
                    deviceHash = deviceClaim;
                }
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = await _licenseService.DeactivateAsync(licenseId, deviceHash, ipAddress, ct);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        public class RenewRequest
        {
            public Guid LicenseId { get; set; }
        }

        /// <summary>
        /// Request license renewal.
        /// </summary>
        [HttpPost("renew")]
        public ActionResult Renew([FromBody] RenewRequest request)
        {
            return Ok(new { success = true, message = "Renewal request received. Please contact your administrator or complete payment." });
        }
    }
}
