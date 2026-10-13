using System;
using System.Collections.Generic;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Entities
{
    public class License
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public LicenseType LicenseType { get; set; }
        public LicenseStatus Status { get; set; }
        public DateTime StartsAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public int MaxDevices { get; set; } = 1;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokeReason { get; set; }

        // Concurrency token for optimistic locking in high concurrency
        public uint RowVersion { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public ICollection<ActivationCode> ActivationCodes { get; set; } = new List<ActivationCode>();
        public ICollection<Device> Devices { get; set; } = new List<Device>();
        public ICollection<LicenseEvent> Events { get; set; } = new List<LicenseEvent>();

        public bool IsValidAt(DateTime utcNow)
        {
            return Status == LicenseStatus.Active &&
                   StartsAtUtc <= utcNow &&
                   ExpiresAtUtc > utcNow;
        }

        public bool CanActivateDevice()
        {
            if (Status != LicenseStatus.Active && Status != LicenseStatus.Pending)
                return false;

            return true;
        }
    }
}
