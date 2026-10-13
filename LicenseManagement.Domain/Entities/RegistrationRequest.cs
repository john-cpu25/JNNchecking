using System;

namespace LicenseManagement.Domain.Entities
{
    public class RegistrationRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"
        public string? ApprovedActivationCode { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
    }
}
