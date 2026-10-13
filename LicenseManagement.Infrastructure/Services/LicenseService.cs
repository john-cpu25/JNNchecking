using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using LicenseManagement.Domain.Entities;
using LicenseManagement.Domain.Enums;
using LicenseManagement.Domain.Services;
using LicenseManagement.Infrastructure.Data;
using LicenseManagement.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseManagement.Infrastructure.Services
{
    public class LicenseService : ILicenseService
    {
        private readonly LicenseDbContext _db;
        private readonly ICodeHasher _codeHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly ILogger<LicenseService> _logger;

        public LicenseService(
            LicenseDbContext db,
            ICodeHasher codeHasher,
            IJwtTokenGenerator tokenGenerator,
            ILogger<LicenseService> logger)
        {
            _db = db;
            _codeHasher = codeHasher;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
        }

        public async Task<ActivateResponse> ActivateAsync(ActivateRequest request, string ipAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.ActivationCode) ||
                string.IsNullOrWhiteSpace(request.DeviceIdentifierHash))
            {
                return new ActivateResponse { Success = false, Message = "Email, activation code, and device identifier are required." };
            }

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            string codeHash = _codeHasher.HashCode(request.ActivationCode);

            // Execute in an atomic transaction to prevent race conditions on multi-device activations
            using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
                if (user == null)
                {
                    return new ActivateResponse { Success = false, Message = "User account not found." };
                }

                if (user.IsBlocked)
                {
                    return new ActivateResponse { Success = false, Message = "User account is suspended." };
                }

                // Find activation code
                var activationCode = await _db.ActivationCodes
                    .Include(a => a.License)
                        .ThenInclude(l => l.Devices)
                    .FirstOrDefaultAsync(a => a.CodeHash == codeHash, ct);

                if (activationCode == null)
                {
                    return new ActivateResponse { Success = false, Message = "Invalid activation code." };
                }

                var license = activationCode.License;
                if (license.UserId != user.Id)
                {
                    return new ActivateResponse { Success = false, Message = "Activation code does not belong to this user account." };
                }

                if (activationCode.Status == ActivationCodeStatus.Revoked)
                {
                    return new ActivateResponse { Success = false, Message = "Activation code has been revoked." };
                }

                if (license.Status == LicenseStatus.Revoked)
                {
                    return new ActivateResponse { Success = false, Message = "License has been revoked." };
                }

                var utcNow = DateTime.UtcNow;

                // Check expiration
                if (license.ExpiresAtUtc <= utcNow)
                {
                    license.Status = LicenseStatus.Expired;
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return new ActivateResponse { Success = false, Message = "License has expired." };
                }

                // Check active devices on this license
                var activeDevices = license.Devices.Where(d => d.Status == DeviceStatus.Active).ToList();
                var existingDevice = activeDevices.FirstOrDefault(d => d.DeviceIdentifierHash == request.DeviceIdentifierHash);

                Device deviceToUse;

                if (existingDevice != null)
                {
                    // Same device reactivating / refreshing session
                    existingDevice.LastValidatedAtUtc = utcNow;
                    existingDevice.DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? existingDevice.DeviceName : request.DeviceName;
                    deviceToUse = existingDevice;
                }
                else
                {
                    // New device attempting activation: check max device limit
                    if (activeDevices.Count >= license.MaxDevices)
                    {
                        await RecordEventAsync(user.Id, license.Id, null, LicenseEventType.ValidationFailed,
                            $"Activation rejected: Max devices reached ({activeDevices.Count}/{license.MaxDevices}). Active on another device.", ipAddress, ct);
                        await _db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);

                        return new ActivateResponse
                        {
                            Success = false,
                            Message = "License is already active on another device. Please deactivate your previous device before activating a new one."
                        };
                    }

                    // Register new device
                    deviceToUse = new Device
                    {
                        LicenseId = license.Id,
                        DeviceIdentifierHash = request.DeviceIdentifierHash,
                        DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? "Unknown Device" : request.DeviceName,
                        Status = DeviceStatus.Active,
                        ActivatedAtUtc = utcNow,
                        LastValidatedAtUtc = utcNow
                    };
                    _db.Devices.Add(deviceToUse);
                }

                // Mark activation code used and license active
                activationCode.Status = ActivationCodeStatus.Used;
                activationCode.UsedAtUtc = utcNow;
                license.Status = LicenseStatus.Active;
                license.UpdatedAtUtc = utcNow;

                await RecordEventAsync(user.Id, license.Id, deviceToUse.Id, LicenseEventType.Activated,
                    $"Activated device '{deviceToUse.DeviceName}' with license {license.LicenseType}.", ipAddress, ct);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Generate JWT client token
                string token = _tokenGenerator.GenerateClientToken(user, license, deviceToUse);

                return new ActivateResponse
                {
                    Success = true,
                    AccessToken = token,
                    LicenseId = license.Id,
                    LicenseType = license.LicenseType,
                    LicenseStatus = license.Status,
                    StartsAtUtc = license.StartsAtUtc,
                    ExpiresAtUtc = license.ExpiresAtUtc,
                    Message = "License activated successfully."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Activation error for user {Email}", request.Email);
                return new ActivateResponse { Success = false, Message = "Activation failed due to an internal server error." };
            }
        }

        public async Task<ValidateResponse> ValidateAsync(Guid licenseId, string deviceHash, string ipAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(deviceHash))
            {
                return new ValidateResponse { IsValid = false, Message = "Device identifier is required." };
            }

            var license = await _db.Licenses
                .Include(l => l.User)
                .Include(l => l.Devices)
                .FirstOrDefaultAsync(l => l.Id == licenseId, ct);

            if (license == null)
            {
                return new ValidateResponse { IsValid = false, Message = "License not found." };
            }

            if (license.User.IsBlocked)
            {
                return new ValidateResponse { IsValid = false, Message = "User account has been suspended." };
            }

            var utcNow = DateTime.UtcNow;

            // Check expiration
            if (utcNow >= license.ExpiresAtUtc)
            {
                if (license.Status != LicenseStatus.Expired)
                {
                    license.Status = LicenseStatus.Expired;
                    license.UpdatedAtUtc = utcNow;
                    await RecordEventAsync(license.UserId, license.Id, null, LicenseEventType.Suspended, "License expired automatically.", ipAddress, ct);
                    await _db.SaveChangesAsync(ct);
                }

                return new ValidateResponse
                {
                    IsValid = false,
                    LicenseType = license.LicenseType,
                    LicenseStatus = LicenseStatus.Expired,
                    StartsAtUtc = license.StartsAtUtc,
                    ExpiresAtUtc = license.ExpiresAtUtc,
                    DaysRemaining = 0,
                    Message = "License has expired. Please renew your license."
                };
            }

            if (license.Status != LicenseStatus.Active)
            {
                return new ValidateResponse
                {
                    IsValid = false,
                    LicenseType = license.LicenseType,
                    LicenseStatus = license.Status,
                    StartsAtUtc = license.StartsAtUtc,
                    ExpiresAtUtc = license.ExpiresAtUtc,
                    DaysRemaining = (int)Math.Max(0, (license.ExpiresAtUtc - utcNow).TotalDays),
                    Message = $"License status is {license.Status}."
                };
            }

            // Check device
            var device = license.Devices.FirstOrDefault(d => d.DeviceIdentifierHash == deviceHash && d.Status == DeviceStatus.Active);
            if (device == null)
            {
                return new ValidateResponse
                {
                    IsValid = false,
                    Message = "This device is not registered or has been deactivated for this license."
                };
            }

            // Update heartbeat validation timestamp
            device.LastValidatedAtUtc = utcNow;
            license.UpdatedAtUtc = utcNow;
            await _db.SaveChangesAsync(ct);

            int daysRemaining = (int)Math.Ceiling((license.ExpiresAtUtc - utcNow).TotalDays);

            return new ValidateResponse
            {
                IsValid = true,
                LicenseType = license.LicenseType,
                LicenseStatus = license.Status,
                StartsAtUtc = license.StartsAtUtc,
                ExpiresAtUtc = license.ExpiresAtUtc,
                DaysRemaining = daysRemaining,
                Message = "License is valid."
            };
        }

        public async Task<DeactivateResponse> DeactivateAsync(Guid licenseId, string deviceHash, string ipAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(deviceHash))
            {
                return new DeactivateResponse { Success = false, Message = "Device identifier is required." };
            }

            var device = await _db.Devices
                .Include(d => d.License)
                .FirstOrDefaultAsync(d => d.LicenseId == licenseId && d.DeviceIdentifierHash == deviceHash && d.Status == DeviceStatus.Active, ct);

            if (device == null)
            {
                return new DeactivateResponse { Success = false, Message = "Active device not found for this license." };
            }

            var utcNow = DateTime.UtcNow;
            device.Status = DeviceStatus.Deactivated;
            device.DeactivatedAtUtc = utcNow;

            await RecordEventAsync(device.License.UserId, licenseId, device.Id, LicenseEventType.DeviceDeactivated,
                $"Device '{device.DeviceName}' deactivated by client.", ipAddress, ct);

            await _db.SaveChangesAsync(ct);

            return new DeactivateResponse { Success = true, Message = "Device deactivated successfully." };
        }

        public async Task<ActivateResponse> StartTrialAsync(CreateTrialRequest request, string ipAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DeviceIdentifierHash))
            {
                return new ActivateResponse { Success = false, Message = "Email and device identifier are required." };
            }

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var utcNow = DateTime.UtcNow;

            using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
                if (user == null)
                {
                    user = new User
                    {
                        Email = request.Email.Trim(),
                        NormalizedEmail = normalizedEmail,
                        CreatedAtUtc = utcNow,
                        UpdatedAtUtc = utcNow
                    };
                    _db.Users.Add(user);
                    await _db.SaveChangesAsync(ct);
                }

                if (user.IsBlocked)
                {
                    return new ActivateResponse { Success = false, Message = "User account is suspended." };
                }

                // Check if user already had a trial
                bool existingTrial = await _db.Licenses.AnyAsync(l => l.UserId == user.Id && l.LicenseType == LicenseType.Trial, ct);
                if (existingTrial)
                {
                    return new ActivateResponse { Success = false, Message = "A trial license has already been issued for this account." };
                }

                // Also check if this device was already used for a trial across any account
                bool deviceHadTrial = await _db.Devices
                    .Include(d => d.License)
                    .AnyAsync(d => d.DeviceIdentifierHash == request.DeviceIdentifierHash && d.License.LicenseType == LicenseType.Trial, ct);

                if (deviceHadTrial)
                {
                    return new ActivateResponse { Success = false, Message = "This device has already used a trial license." };
                }

                // Create Trial License: 15 days
                var startsAtUtc = utcNow;
                var expiresAtUtc = CalendarDurationCalculator.CalculateTrialExpiration(startsAtUtc);

                var license = new License
                {
                    UserId = user.Id,
                    LicenseType = LicenseType.Trial,
                    Status = LicenseStatus.Active,
                    StartsAtUtc = startsAtUtc,
                    ExpiresAtUtc = expiresAtUtc,
                    MaxDevices = 1,
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                };
                _db.Licenses.Add(license);

                // Add active device directly for trial
                var device = new Device
                {
                    LicenseId = license.Id,
                    DeviceIdentifierHash = request.DeviceIdentifierHash,
                    DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? "Trial Machine" : request.DeviceName,
                    Status = DeviceStatus.Active,
                    ActivatedAtUtc = utcNow,
                    LastValidatedAtUtc = utcNow
                };
                _db.Devices.Add(device);

                await RecordEventAsync(user.Id, license.Id, device.Id, LicenseEventType.TrialStarted,
                    $"15-day trial started for '{user.Email}'.", ipAddress, ct);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                string token = _tokenGenerator.GenerateClientToken(user, license, device);

                return new ActivateResponse
                {
                    Success = true,
                    AccessToken = token,
                    LicenseId = license.Id,
                    LicenseType = license.LicenseType,
                    LicenseStatus = license.Status,
                    StartsAtUtc = license.StartsAtUtc,
                    ExpiresAtUtc = license.ExpiresAtUtc,
                    Message = "Trial activated successfully for 15 days."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Trial creation error for {Email}", request.Email);
                return new ActivateResponse { Success = false, Message = "Failed to start trial." };
            }
        }

        public async Task<ValidateResponse> GetLicenseStatusAsync(Guid licenseId, string deviceHash, CancellationToken ct = default)
        {
            return await ValidateAsync(licenseId, deviceHash, "local", ct);
        }

        public async Task<RegisterAccountResponse> SubmitRegistrationRequestAsync(RegisterAccountRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return new RegisterAccountResponse { Success = false, Message = "Email là bắt buộc." };
            }

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var utcNow = DateTime.UtcNow;

            // Check if user already exists or create user record
            var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
            if (user == null)
            {
                user = new User
                {
                    Email = request.Email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    FullName = request.FullName?.Trim(),
                    PhoneNumber = request.PhoneNumber?.Trim(),
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                };
                _db.Users.Add(user);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
                if (!string.IsNullOrWhiteSpace(request.PhoneNumber)) user.PhoneNumber = request.PhoneNumber.Trim();
                user.UpdatedAtUtc = utcNow;
            }

            // Check if there is an existing pending or approved registration request
            var existingReq = await _db.RegistrationRequests
                .FirstOrDefaultAsync(r => r.NormalizedEmail == normalizedEmail, ct);

            if (existingReq != null)
            {
                if (existingReq.Status == "Approved" && !string.IsNullOrEmpty(existingReq.ApprovedActivationCode))
                {
                    return new RegisterAccountResponse
                    {
                        Success = true,
                        AlreadyApproved = true,
                        ActivationCode = existingReq.ApprovedActivationCode,
                        Message = "Tài khoản của bạn đã được Quản trị viên duyệt!"
                    };
                }

                // Update request info with latest machine ID / phone
                existingReq.FullName = request.FullName ?? existingReq.FullName;
                existingReq.PhoneNumber = request.PhoneNumber ?? existingReq.PhoneNumber;
                existingReq.MachineId = request.MachineId ?? existingReq.MachineId;
                existingReq.DeviceName = request.DeviceName ?? existingReq.DeviceName;
                existingReq.Status = "Pending";
                existingReq.CreatedAtUtc = utcNow;
            }
            else
            {
                var newReq = new RegistrationRequest
                {
                    Email = request.Email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    FullName = request.FullName ?? string.Empty,
                    PhoneNumber = request.PhoneNumber ?? string.Empty,
                    MachineId = request.MachineId ?? string.Empty,
                    DeviceName = request.DeviceName ?? string.Empty,
                    CreatedAtUtc = utcNow,
                    Status = "Pending"
                };
                _db.RegistrationRequests.Add(newReq);
            }

            await _db.SaveChangesAsync(ct);

            return new RegisterAccountResponse
            {
                Success = true,
                AlreadyApproved = false,
                Message = "Đã gửi thông tin đăng ký lên hệ thống máy chủ thành công! Quản trị viên đang xem xét phê duyệt."
            };
        }

        public async Task<RegisterAccountResponse> GetRegistrationStatusAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new RegisterAccountResponse { Success = false };

            string normalizedEmail = email.Trim().ToLowerInvariant();
            var req = await _db.RegistrationRequests.FirstOrDefaultAsync(r => r.NormalizedEmail == normalizedEmail, ct);

            if (req == null)
            {
                return new RegisterAccountResponse { Success = false, Message = "Chưa có yêu cầu đăng ký nào." };
            }

            if (req.Status == "Approved" && !string.IsNullOrEmpty(req.ApprovedActivationCode))
            {
                return new RegisterAccountResponse
                {
                    Success = true,
                    AlreadyApproved = true,
                    ActivationCode = req.ApprovedActivationCode,
                    Message = "Tài khoản đã được phê duyệt!"
                };
            }

            return new RegisterAccountResponse
            {
                Success = true,
                AlreadyApproved = false,
                Message = req.Status == "Rejected" ? "Yêu cầu đã bị từ chối." : "Yêu cầu đang chờ duyệt."
            };
        }

        private async Task RecordEventAsync(Guid? userId, Guid? licenseId, Guid? deviceId, LicenseEventType eventType, string details, string? ipAddress, CancellationToken ct)
        {
            var ev = new LicenseEvent
            {
                UserId = userId,
                LicenseId = licenseId,
                DeviceId = deviceId,
                EventType = eventType,
                EventDetails = details,
                IpAddress = ipAddress,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.LicenseEvents.Add(ev);
        }
    }
}
