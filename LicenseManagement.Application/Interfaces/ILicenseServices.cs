using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Application.Interfaces
{
    public interface ILicenseService
    {
        Task<ActivateResponse> ActivateAsync(ActivateRequest request, string ipAddress, CancellationToken ct = default);
        Task<ValidateResponse> ValidateAsync(Guid licenseId, string deviceHash, string ipAddress, CancellationToken ct = default);
        Task<DeactivateResponse> DeactivateAsync(Guid licenseId, string deviceHash, string ipAddress, CancellationToken ct = default);
        Task<ActivateResponse> StartTrialAsync(CreateTrialRequest request, string ipAddress, CancellationToken ct = default);
        Task<ValidateResponse> GetLicenseStatusAsync(Guid licenseId, string deviceHash, CancellationToken ct = default);
        Task<RegisterAccountResponse> SubmitRegistrationRequestAsync(RegisterAccountRequest request, CancellationToken ct = default);
        Task<RegisterAccountResponse> GetRegistrationStatusAsync(string email, CancellationToken ct = default);
    }

    public interface IAdminService
    {
        Task<AdminLoginResponse> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default);
        Task<CreateLicenseResponse> CreateLicenseAsync(CreateLicenseRequest request, CancellationToken ct = default);
        Task<bool> RenewLicenseAsync(Guid licenseId, CancellationToken ct = default);
        Task<bool> RevokeLicenseAsync(Guid licenseId, string reason, CancellationToken ct = default);
        Task<bool> ResetDeviceAsync(Guid licenseId, Guid deviceId, CancellationToken ct = default);
        Task<bool> SetUserBlockStatusAsync(Guid userId, bool isBlocked, CancellationToken ct = default);
        Task<List<LicenseSummaryDto>> GetLicensesAsync(string? search = null, CancellationToken ct = default);
        Task<List<LicenseEventDto>> GetEventsAsync(int limit = 100, CancellationToken ct = default);
        Task<List<RegistrationRequestDto>> GetPendingRegistrationRequestsAsync(CancellationToken ct = default);
        Task<CreateLicenseResponse?> ApproveRegistrationRequestAsync(Guid requestId, LicenseType licenseType, int maxDevices = 1, CancellationToken ct = default);
        Task<bool> RejectRegistrationRequestAsync(Guid requestId, CancellationToken ct = default);
    }
}
