using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Services
{
    /// <summary>
    /// Explicit state transition model for License lifecycle:
    /// Pending -> Active | Revoked
    /// Active -> Expired | Revoked | Suspended
    /// Expired -> Active (via renewal) | Revoked
    /// Suspended -> Active (via unblock/reinstate) | Revoked
    /// Revoked -> Terminal state (no transitions permitted)
    /// </summary>
    public static class LicenseStateMachine
    {
        public static bool CanTransition(LicenseStatus current, LicenseStatus target)
        {
            if (current == target) return true;

            return (current, target) switch
            {
                (LicenseStatus.Pending, LicenseStatus.Active) => true,
                (LicenseStatus.Pending, LicenseStatus.Revoked) => true,

                (LicenseStatus.Active, LicenseStatus.Expired) => true,
                (LicenseStatus.Active, LicenseStatus.Suspended) => true,
                (LicenseStatus.Active, LicenseStatus.Revoked) => true,

                (LicenseStatus.Expired, LicenseStatus.Active) => true, // Allowed only through renewal
                (LicenseStatus.Expired, LicenseStatus.Revoked) => true,

                (LicenseStatus.Suspended, LicenseStatus.Active) => true, // Unblock/reinstate
                (LicenseStatus.Suspended, LicenseStatus.Revoked) => true,

                (LicenseStatus.Revoked, _) => false, // Terminal: Cannot transition out of revoked

                _ => false
            };
        }

        public static void EnsureValidTransition(LicenseStatus current, LicenseStatus target)
        {
            if (!CanTransition(current, target))
            {
                throw new InvalidOperationException($"Invalid license state transition from '{current}' to '{target}'.");
            }
        }
    }
}
