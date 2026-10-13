using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Entities
{
    public class AdminUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public AdminRole Role { get; set; } = AdminRole.Admin;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAtUtc { get; set; }
    }
}
