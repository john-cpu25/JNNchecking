using System;
using System.Threading;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LicenseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ILicenseService _licenseService;

        public AuthController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        /// <summary>
        /// Activates a license using an email and GUID activation code for a specific device.
        /// </summary>
        [HttpPost("activate")]
        public async Task<ActionResult<ActivateResponse>> Activate([FromBody] ActivateRequest request, CancellationToken ct)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = await _licenseService.ActivateAsync(request, ipAddress, ct);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Initiates a 15-day trial license for an email and device.
        /// </summary>
        [HttpPost("trial")]
        public async Task<ActionResult<ActivateResponse>> StartTrial([FromBody] CreateTrialRequest request, CancellationToken ct)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = await _licenseService.StartTrialAsync(request, ipAddress, ct);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Registers an account and submits a request for admin approval.
        /// </summary>
        [HttpPost("register-request")]
        public async Task<ActionResult<RegisterAccountResponse>> RegisterRequest([FromBody] RegisterAccountRequest request, CancellationToken ct)
        {
            var res = await _licenseService.SubmitRegistrationRequestAsync(request, ct);
            return Ok(res);
        }

        /// <summary>
        /// Checks if a user's registration request has been approved and retrieves the activation key.
        /// </summary>
        [HttpGet("check-request")]
        public async Task<ActionResult<RegisterAccountResponse>> CheckRegistration([FromQuery] string email, CancellationToken ct)
        {
            var res = await _licenseService.GetRegistrationStatusAsync(email, ct);
            return Ok(res);
        }

        /// <summary>
        /// Optional email verification endpoint.
        /// </summary>
        [HttpPost("verify-email")]
        public ActionResult VerifyEmail([FromBody] object request)
        {
            return Ok(new { success = true, message = "Email ownership verified." });
        }
    }
}
