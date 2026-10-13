using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Application.DTOs
{
    public class ActivateRequest
    {
        public string Email { get; set; } = string.Empty;
        public string ActivationCode { get; set; } = string.Empty;
        public string DeviceIdentifierHash { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class ActivateResponse
    {
        public bool Success { get; set; }
        public string? AccessToken { get; set; }
        public Guid? LicenseId { get; set; }
        public LicenseType? LicenseType { get; set; }
        public LicenseStatus? LicenseStatus { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ValidateRequest
    {
        public string DeviceIdentifierHash { get; set; } = string.Empty;
    }

    public class ValidateResponse
    {
        public bool IsValid { get; set; }
        public LicenseType? LicenseType { get; set; }
        public LicenseStatus? LicenseStatus { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public int DaysRemaining { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DeactivateRequest
    {
        public string DeviceIdentifierHash { get; set; } = string.Empty;
    }

    public class DeactivateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CreateTrialRequest
    {
        public string Email { get; set; } = string.Empty;
        public string DeviceIdentifierHash { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class CreateLicenseRequest
    {
        public string Email { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; } = LicenseType.Annual;
        public int MaxDevices { get; set; } = 1;
    }

    public class CreateLicenseResponse
    {
        public Guid LicenseId { get; set; }
        /// <summary>
        /// Plaintext GUID code returned ONCE upon creation. Never stored plaintext in DB.
        /// </summary>
        public string ActivationCode { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; }
        public DateTime StartsAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class AdminLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AdminLoginResponse
    {
        public bool Success { get; set; }
        public string? AccessToken { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class LicenseSummaryDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; }
        public LicenseStatus Status { get; set; }
        public DateTime StartsAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public int MaxDevices { get; set; }
        public int ActiveDeviceCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<DeviceSummaryDto> Devices { get; set; } = new();
    }

    public class DeviceSummaryDto
    {
        public Guid Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceIdentifierHash { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; }
        public DateTime ActivatedAtUtc { get; set; }
        public DateTime LastValidatedAtUtc { get; set; }
    }

    public class LicenseEventDto
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public Guid? LicenseId { get; set; }
        public Guid? DeviceId { get; set; }
        public LicenseEventType EventType { get; set; }
        public string EventDetails { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class RegisterAccountRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class RegisterAccountResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool AlreadyApproved { get; set; }
        public string? ActivationCode { get; set; }
    }

    public class RegistrationRequestDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ApprovedActivationCode { get; set; }
    }
}
