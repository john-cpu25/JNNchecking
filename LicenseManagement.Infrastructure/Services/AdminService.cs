using System;
using System.Collections.Generic;
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
    public class AdminService : IAdminService
    {
        private readonly LicenseDbContext _db;
        private readonly ICodeHasher _codeHasher;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            LicenseDbContext db,
            ICodeHasher codeHasher,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator,
            ILogger<AdminService> logger)
        {
            _db = db;
            _codeHasher = codeHasher;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
        }

        public async Task<AdminLoginResponse> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return new AdminLoginResponse { Success = false, Message = "Email and password are required." };
            }

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.NormalizedEmail == normalizedEmail, ct);

            if (admin == null || !admin.IsActive)
            {
                return new AdminLoginResponse { Success = false, Message = "Invalid admin credentials." };
            }

            if (!_passwordHasher.VerifyPassword(request.Password, admin.PasswordHash))
            {
                return new AdminLoginResponse { Success = false, Message = "Invalid admin credentials." };
            }

            admin.LastLoginAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            string token = _tokenGenerator.GenerateAdminToken(admin);

            return new AdminLoginResponse
            {
                Success = true,
                AccessToken = token,
                Email = admin.Email,
                Role = admin.Role.ToString(),
                Message = "Authentication successful."
            };
        }

        public async Task<CreateLicenseResponse> CreateLicenseAsync(CreateLicenseRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email is required.", nameof(request.Email));
            }

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var utcNow = DateTime.UtcNow;

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

            var startsAtUtc = utcNow;
            DateTime expiresAtUtc = request.LicenseType == LicenseType.Trial
                ? CalendarDurationCalculator.CalculateTrialExpiration(startsAtUtc)
                : CalendarDurationCalculator.CalculateAnnualExpiration(startsAtUtc);

            var license = new License
            {
                UserId = user.Id,
                LicenseType = request.LicenseType,
                Status = LicenseStatus.Pending,
                StartsAtUtc = startsAtUtc,
                ExpiresAtUtc = expiresAtUtc,
                MaxDevices = Math.Max(1, request.MaxDevices),
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };
            _db.Licenses.Add(license);

            // Generate clean GUID activation code
            string rawActivationCode = Guid.NewGuid().ToString();
            string codeHash = _codeHasher.HashCode(rawActivationCode);

            var activationCode = new ActivationCode
            {
                LicenseId = license.Id,
                CodeHash = codeHash,
                Status = ActivationCodeStatus.Unused,
                CreatedAtUtc = utcNow
            };
            _db.ActivationCodes.Add(activationCode);

            var ev = new LicenseEvent
            {
                UserId = user.Id,
                LicenseId = license.Id,
                EventType = LicenseEventType.Created,
                EventDetails = $"Created {request.LicenseType} license. Activation code generated.",
                CreatedAtUtc = utcNow
            };
            _db.LicenseEvents.Add(ev);

            await _db.SaveChangesAsync(ct);

            return new CreateLicenseResponse
            {
                LicenseId = license.Id,
                ActivationCode = rawActivationCode, // Displayed ONLY once to admin
                LicenseType = license.LicenseType,
                StartsAtUtc = license.StartsAtUtc,
                ExpiresAtUtc = license.ExpiresAtUtc
            };
        }

        public async Task<bool> RenewLicenseAsync(Guid licenseId, CancellationToken ct = default)
        {
            var license = await _db.Licenses
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == licenseId, ct);

            if (license == null) return false;

            var utcNow = DateTime.UtcNow;
            LicenseStateMachine.EnsureValidTransition(license.Status, LicenseStatus.Active);

            var (newStarts, newExpires) = CalendarDurationCalculator.CalculateRenewal(
                license.StartsAtUtc,
                license.ExpiresAtUtc,
                utcNow);

            license.StartsAtUtc = newStarts;
            license.ExpiresAtUtc = newExpires;
            license.Status = LicenseStatus.Active;
            license.UpdatedAtUtc = utcNow;

            var ev = new LicenseEvent
            {
                UserId = license.UserId,
                LicenseId = license.Id,
                EventType = LicenseEventType.Renewed,
                EventDetails = $"License renewed until {newExpires:yyyy-MM-dd HH:mm:ss} UTC.",
                CreatedAtUtc = utcNow
            };
            _db.LicenseEvents.Add(ev);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RevokeLicenseAsync(Guid licenseId, string reason, CancellationToken ct = default)
        {
            var license = await _db.Licenses
                .Include(l => l.ActivationCodes)
                .Include(l => l.Devices)
                .FirstOrDefaultAsync(l => l.Id == licenseId, ct);

            if (license == null) return false;

            var utcNow = DateTime.UtcNow;
            LicenseStateMachine.EnsureValidTransition(license.Status, LicenseStatus.Revoked);

            license.Status = LicenseStatus.Revoked;
            license.RevokedAtUtc = utcNow;
            license.RevokeReason = reason;
            license.UpdatedAtUtc = utcNow;

            foreach (var code in license.ActivationCodes)
            {
                code.Status = ActivationCodeStatus.Revoked;
                code.RevokedAtUtc = utcNow;
            }

            foreach (var device in license.Devices)
            {
                device.Status = DeviceStatus.Revoked;
                device.DeactivatedAtUtc = utcNow;
            }

            var ev = new LicenseEvent
            {
                UserId = license.UserId,
                LicenseId = license.Id,
                EventType = LicenseEventType.Revoked,
                EventDetails = $"License revoked. Reason: {reason}",
                CreatedAtUtc = utcNow
            };
            _db.LicenseEvents.Add(ev);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ResetDeviceAsync(Guid licenseId, Guid deviceId, CancellationToken ct = default)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.LicenseId == licenseId && d.Id == deviceId, ct);
            if (device == null) return false;

            var utcNow = DateTime.UtcNow;
            device.Status = DeviceStatus.Deactivated;
            device.DeactivatedAtUtc = utcNow;

            var ev = new LicenseEvent
            {
                LicenseId = licenseId,
                DeviceId = deviceId,
                EventType = LicenseEventType.DeviceReset,
                EventDetails = $"Device '{device.DeviceName}' reset by administrator.",
                CreatedAtUtc = utcNow
            };
            _db.LicenseEvents.Add(ev);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> SetUserBlockStatusAsync(Guid userId, bool isBlocked, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null) return false;

            user.IsBlocked = isBlocked;
            user.UpdatedAtUtc = DateTime.UtcNow;

            var ev = new LicenseEvent
            {
                UserId = userId,
                EventType = isBlocked ? LicenseEventType.UserBlocked : LicenseEventType.UserUnblocked,
                EventDetails = isBlocked ? "User account blocked by administrator." : "User account unblocked by administrator.",
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.LicenseEvents.Add(ev);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<LicenseSummaryDto>> GetLicensesAsync(string? search = null, CancellationToken ct = default)
        {
            var query = _db.Licenses
                .Include(l => l.User)
                .Include(l => l.Devices)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLowerInvariant();
                query = query.Where(l => l.User.NormalizedEmail.Contains(s));
            }

            var list = await query
                .OrderByDescending(l => l.CreatedAtUtc)
                .Select(l => new LicenseSummaryDto
                {
                    Id = l.Id,
                    UserId = l.UserId,
                    Email = l.User.Email,
                    LicenseType = l.LicenseType,
                    Status = l.Status,
                    StartsAtUtc = l.StartsAtUtc,
                    ExpiresAtUtc = l.ExpiresAtUtc,
                    MaxDevices = l.MaxDevices,
                    ActiveDeviceCount = l.Devices.Count(d => d.Status == DeviceStatus.Active),
                    CreatedAtUtc = l.CreatedAtUtc,
                    Devices = l.Devices.Select(d => new DeviceSummaryDto
                    {
                        Id = d.Id,
                        DeviceName = d.DeviceName,
                        DeviceIdentifierHash = d.DeviceIdentifierHash,
                        Status = d.Status,
                        ActivatedAtUtc = d.ActivatedAtUtc,
                        LastValidatedAtUtc = d.LastValidatedAtUtc
                    }).ToList()
                })
                .ToListAsync(ct);

            return list;
        }

        public async Task<List<LicenseEventDto>> GetEventsAsync(int limit = 100, CancellationToken ct = default)
        {
            return await _db.LicenseEvents
                .Include(e => e.User)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(limit)
                .Select(e => new LicenseEventDto
                {
                    Id = e.Id,
                    Email = e.User != null ? e.User.Email : null,
                    LicenseId = e.LicenseId,
                    DeviceId = e.DeviceId,
                    EventType = e.EventType,
                    EventDetails = e.EventDetails,
                    IpAddress = e.IpAddress,
                    CreatedAtUtc = e.CreatedAtUtc
                })
                .ToListAsync(ct);
        }

        public async Task<List<RegistrationRequestDto>> GetPendingRegistrationRequestsAsync(CancellationToken ct = default)
        {
            return await _db.RegistrationRequests
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(r => new RegistrationRequestDto
                {
                    Id = r.Id,
                    Email = r.Email,
                    FullName = r.FullName,
                    PhoneNumber = r.PhoneNumber,
                    MachineId = r.MachineId,
                    DeviceName = r.DeviceName,
                    CreatedAtUtc = r.CreatedAtUtc,
                    Status = r.Status,
                    ApprovedActivationCode = r.ApprovedActivationCode
                })
                .ToListAsync(ct);
        }

        public async Task<CreateLicenseResponse?> ApproveRegistrationRequestAsync(Guid requestId, LicenseType licenseType, int maxDevices = 1, CancellationToken ct = default)
        {
            var req = await _db.RegistrationRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
            if (req == null) return null;

            // Create license for user
            var licRes = await CreateLicenseAsync(new CreateLicenseRequest
            {
                Email = req.Email,
                LicenseType = licenseType,
                MaxDevices = maxDevices
            }, ct);

            req.Status = "Approved";
            req.ApprovedActivationCode = licRes.ActivationCode;
            req.ProcessedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return licRes;
        }

        public async Task<bool> RejectRegistrationRequestAsync(Guid requestId, CancellationToken ct = default)
        {
            var req = await _db.RegistrationRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
            if (req == null) return false;

            req.Status = "Rejected";
            req.ProcessedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
