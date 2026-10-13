using System;
using System.Collections.Generic;

namespace LicenseManagement.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool EmailVerified { get; set; }
        public string? EmailVerificationToken { get; set; }
        public DateTime? VerificationTokenExpiresAtUtc { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<License> Licenses { get; set; } = new List<License>();
        public ICollection<LicenseEvent> Events { get; set; } = new List<LicenseEvent>();
    }
}
