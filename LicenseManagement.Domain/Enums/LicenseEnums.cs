using System;

namespace LicenseManagement.Domain.Enums
{
    public enum LicenseType
    {
        Trial = 1,
        Annual = 2
    }

    public enum LicenseStatus
    {
        Pending = 1,
        Active = 2,
        Expired = 3,
        Revoked = 4,
        Suspended = 5
    }

    public enum ActivationCodeStatus
    {
        Unused = 1,
        Used = 2,
        Revoked = 3,
        Expired = 4
    }

    public enum DeviceStatus
    {
        Active = 1,
        Deactivated = 2,
        Revoked = 3
    }

    public enum LicenseEventType
    {
        Created = 1,
        Activated = 2,
        Validated = 3,
        Renewed = 4,
        Revoked = 5,
        Suspended = 6,
        DeviceReset = 7,
        DeviceDeactivated = 8,
        UserBlocked = 9,
        UserUnblocked = 10,
        ValidationFailed = 11,
        TrialStarted = 12
    }

    public enum AdminRole
    {
        SuperAdmin = 1,
        Admin = 2,
        Support = 3
    }
}
