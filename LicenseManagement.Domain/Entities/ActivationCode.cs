using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Entities
{
    public class ActivationCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LicenseId { get; set; }
        
        /// <summary>
        /// Salted cryptographic hash of the GUID activation code. Never plaintext.
        /// </summary>
        public string CodeHash { get; set; } = string.Empty;

        public ActivationCodeStatus Status { get; set; } = ActivationCodeStatus.Unused;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }

        // Navigation
        public License License { get; set; } = null!;
    }
}
