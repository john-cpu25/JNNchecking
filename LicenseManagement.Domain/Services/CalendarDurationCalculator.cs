using System;
using LicenseManagement.Domain.Enums;

namespace LicenseManagement.Domain.Services
{
    /// <summary>
    /// Pure domain calculator for trial and annual license durations.
    /// Ensures strict UTC calendar calculations, leap year safety, and renewal boundary rules.
    /// </summary>
    public static class CalendarDurationCalculator
    {
        public const int TrialDurationDays = 30;
        public const int AnnualDurationYears = 1;

        /// <summary>
        /// Calculates trial expiration: exactly 30 calendar days from start in UTC.
        /// </summary>
        public static DateTime CalculateTrialExpiration(DateTime startsAtUtc)
        {
            if (startsAtUtc.Kind != DateTimeKind.Utc)
            {
                startsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
            }
            return startsAtUtc.AddDays(TrialDurationDays);
        }

        /// <summary>
        /// Calculates annual expiration: exactly 1 calendar year from start in UTC.
        /// Handles leap year boundaries safely (e.g., Feb 29 -> Feb 28 of non-leap year).
        /// </summary>
        public static DateTime CalculateAnnualExpiration(DateTime startsAtUtc)
        {
            if (startsAtUtc.Kind != DateTimeKind.Utc)
            {
                startsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
            }
            return startsAtUtc.AddYears(AnnualDurationYears);
        }

        /// <summary>
        /// Calculates renewal expiration for an annual license.
        /// If the license is still active and has not expired, extends by 1 calendar year from current expiry.
        /// If the license is already expired, extends by 1 calendar year from utcNow.
        /// </summary>
        public static (DateTime newStartsAtUtc, DateTime newExpiresAtUtc) CalculateRenewal(
            DateTime currentStartsAtUtc,
            DateTime currentExpiresAtUtc,
            DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            }

            if (currentExpiresAtUtc > utcNow)
            {
                // Active: extend seamlessly from the existing expiration boundary
                DateTime newExpiry = currentExpiresAtUtc.AddYears(AnnualDurationYears);
                return (currentStartsAtUtc, newExpiry);
            }
            else
            {
                // Expired: reset period starting from right now (utcNow)
                DateTime newExpiry = utcNow.AddYears(AnnualDurationYears);
                return (utcNow, newExpiry);
            }
        }
    }
}
