using System.Text.Json;

namespace SemanticKernelPoc.Api.Services.Helpers;

public static class CalendarHelpers
{
    /// <summary>
    /// Format calendar events for frontend card rendering
    /// </summary>
    public static string FormatCalendarCardsResponse(IEnumerable<object> events, string userName, string timeRange, int totalCount)
    {
        var calendarResponse = new
        {
            Type = "calendar_events",
            Count = totalCount,
            UserName = userName,
            TimeRange = timeRange,
            Events = events
        };

        return $"CALENDAR_CARDS:{JsonSerializer.Serialize(calendarResponse, new JsonSerializerOptions { WriteIndented = false })}";
    }

    /// <summary>
    /// Create calendar event object for consistent formatting
    /// </summary>
    public static object CreateEventObject(
        string subject,
        string startDateTime,
        string endDateTime,
        string location,
        string organizer,
        bool isAllDay,
        string id)
    {
        return new
        {
            Subject = subject,
            Start = startDateTime,
            End = endDateTime,
            Location = string.IsNullOrWhiteSpace(location) ? "No location" : location,
            Organizer = organizer ?? "Unknown",
            IsAllDay = isAllDay,
            Id = id
        };
    }

    /// <summary>
    /// Try to parse various date/time formats including natural language
    /// </summary>
    public static bool TryParseDateTime(string input, out DateTime result)
    {
        result = default;

        // Try standard formats first
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "MM/dd/yyyy HH:mm",
            "MM/dd/yyyy H:mm",
            "yyyy-MM-dd",
            "MM/dd/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out result))
            {
                return true;
            }
        }

        // Try natural language parsing
        var normalizedInput = input.ToLower().Trim();

        if (normalizedInput.Contains("tomorrow"))
        {
            var tomorrow = DateTime.Today.AddDays(1);
            if (normalizedInput.Contains("morning") || normalizedInput.Contains("9am") || normalizedInput.Contains("9:00"))
            {
                result = tomorrow.AddHours(9);
                return true;
            }
            if (normalizedInput.Contains("afternoon") || normalizedInput.Contains("2pm") || normalizedInput.Contains("14:00"))
            {
                result = tomorrow.AddHours(14);
                return true;
            }
            if (normalizedInput.Contains("evening") || normalizedInput.Contains("6pm") || normalizedInput.Contains("18:00"))
            {
                result = tomorrow.AddHours(18);
                return true;
            }
            // Default tomorrow time
            result = tomorrow.AddHours(9);
            return true;
        }

        if (normalizedInput.Contains("today"))
        {
            var today = DateTime.Today;
            if (normalizedInput.Contains("afternoon"))
            {
                result = today.AddHours(14);
                return true;
            }
            result = today.AddHours(9);
            return true;
        }

        // Try general DateTime.TryParse as fallback
        return DateTime.TryParse(input, out result);
    }

    /// <summary>
    /// Get current week bounds (Monday to Sunday)
    /// </summary>
    private static (DateTime StartTime, DateTime EndTime, string Description) GetCurrentWeekBounds(DateTime today)
    {
        // Find Monday of current week
        var daysFromMonday = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
        if (daysFromMonday < 0) daysFromMonday += 7; // Handle Sunday

        var monday = today.AddDays(-daysFromMonday);
        var sunday = monday.AddDays(7);

        return (monday, sunday, $"this week ({monday:MMM dd} - {sunday.AddDays(-1):MMM dd})");
    }

    /// <summary>
    /// Parse time period string into date range
    /// </summary>
    public static (DateTime StartTime, DateTime EndTime, string Description) ParseTimePeriod(string timePeriod)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;

        return timePeriod.ToLower() switch
        {
            "today" => (today, today.AddDays(1), $"today ({today:yyyy-MM-dd})"),
            "tomorrow" => (today.AddDays(1), today.AddDays(2), $"tomorrow ({today.AddDays(1):yyyy-MM-dd})"),
            "this_week" => GetCurrentWeekBounds(today),
            "this_week_upcoming" => (now, GetCurrentWeekBounds(today).EndTime, $"upcoming this week"),
            "next_week" => (today.AddDays(7), today.AddDays(14), "next week"),
            "this_month" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1), "this month"),
            "this_month_upcoming" => (now, new DateTime(today.Year, today.Month, 1).AddMonths(1), $"upcoming this month"),
            _ => int.TryParse(timePeriod, out var days) ?
                (now, now.AddDays(days), $"next {days} days") :
                (now, now.AddDays(7), "next 7 days")
        };
    }
}