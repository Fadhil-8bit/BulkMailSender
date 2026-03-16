using System;

namespace BulkMailSender.Utilities;

public static class TimeHelper
{
    private static readonly TimeZoneInfo TargetTimeZone = InitializeTimeZone();

    private static TimeZoneInfo InitializeTimeZone()
    {
        // Allow specifying a target timezone via environment variable APP_TIMEZONE.
        // Example values: "Asia/Shanghai" (IANA on Linux) or "China Standard Time" (Windows).
        var tzId = Environment.GetEnvironmentVariable("APP_TIMEZONE");
        if (!string.IsNullOrEmpty(tzId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                // Ignore and fall back to local
            }
        }

        // Default: prefer Malaysia timezone (Asia/Kuala_Lumpur). Try IANA first (Linux), then Windows id.
        var tryIds = new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" };
        foreach (var id in tryIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                // ignore and try next
            }
        }

        // Fallback: use system local timezone
        return TimeZoneInfo.Local;
    }

    public static string FormatTimestamp(DateTime utcTimestamp)
    {
        try
        {
            var timestampUtc = utcTimestamp.Kind == DateTimeKind.Utc
                ? utcTimestamp
                : DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);

            var converted = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, TargetTimeZone);
            return converted.ToString("HH:mm:ss");
        }
        catch
        {
            // Fallback: local representation
            return utcTimestamp.ToLocalTime().ToString("HH:mm:ss");
        }
    }
}
