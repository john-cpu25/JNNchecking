using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Entities
{
    public class LicenseEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; }
        public Guid? LicenseId { get; set; }
        public Guid? DeviceId { get; set; }
        public LicenseEventType EventType { get; set; }
        public string EventDetails { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public License? License { get; set; }
        public Device? Device { get; set; }
    }
}
