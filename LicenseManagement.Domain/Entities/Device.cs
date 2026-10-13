using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Entities
{
    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LicenseId { get; set; }
        
        /// <summary>
        /// SHA-256 hash of device hardware identifiers (CPU + Motherboard + Disk serial). Never raw identifiers.
        /// </summary>
        public string DeviceIdentifierHash { get; set; } = string.Empty;

        public string DeviceName { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; } = DeviceStatus.Active;
        public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastValidatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? DeactivatedAtUtc { get; set; }

        // Navigation
        public License License { get; set; } = null!;
    }
}
