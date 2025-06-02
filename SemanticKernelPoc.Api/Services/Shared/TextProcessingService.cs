using System.Text.RegularExpressions;
using Microsoft.Graph.Models;

namespace SemanticKernelPoc.Api.Services.Shared;

/// <summary>
/// Service to handle common text processing operations and eliminate code duplication
/// </summary>
public interface ITextProcessingService
{
    /// <summary>
    /// Truncate text to specified length with ellipsis
    /// </summary>
    string TruncateText(string? text, int maxLength, string fallback = "");
    
    /// <summary>
    /// Clean HTML content and limit length for AI processing
    /// </summary>
    string CleanAndLimitContent(string? content, int maxLength = Constants.TextLimits.EmailContentForAI);
    
    /// <summary>
    /// Get safe display name with fallback
    /// </summary>
    string GetSafeDisplayName(string? name, string? email, string fallback = Constants.DefaultText.Unknown);
    
    /// <summary>
    /// Get valid email address with fallback
    /// </summary>
    string GetValidEmailAddress(string? email, string fallback = "");
    
    /// <summary>
    /// Format email content by removing HTML and limiting length
    /// </summary>
    string FormatEmailContent(string? content);
    
    /// <summary>
    /// Get color for priority/importance level
    /// </summary>
    string GetPriorityColor(string? priority);
    
    /// <summary>
    /// Get color for read status
    /// </summary>
    string GetReadStatusColor(bool isRead);
    
    /// <summary>
    /// Get color for task status
    /// </summary>
    string GetTaskStatusColor(string? status);
    
    /// <summary>
    /// Parse due date with error handling
    /// </summary>
    (DateTime? Date, bool HasError, string? ErrorMessage) ParseDueDate(string? dueDate);
    
    /// <summary>
    /// Format task due date
    /// </summary>
    string? FormatTaskDueDate(DateTimeTimeZone? dueDateTime, string? format = null);
    
    /// <summary>
    /// Parse time period into date range
    /// </summary>
    (DateTime? startDate, DateTime? endDate) ParseTimePeriod(string? timePeriod);
    
    /// <summary>
    /// Format calendar event date/time
    /// </summary>
    string? FormatEventDateTime(DateTimeTimeZone? dateTime, string? format = null);
    
    /// <summary>
    /// Parse event date/time with error handling
    /// </summary>
    (DateTime? DateTime, bool HasError, string? ErrorMessage) ParseEventDateTime(string? dateTime);
}

/// <summary>
/// Implementation of text processing service
/// </summary>
public class TextProcessingService : ITextProcessingService
{
    public string TruncateText(string? text, int maxLength, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }
        
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    public string CleanAndLimitContent(string? content, int maxLength = Constants.TextLimits.EmailContentForAI)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "";
        }
        
        // Remove HTML tags and extra whitespace
        var cleaned = Regex.Replace(content, "<[^>]*>", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        
        return TruncateText(cleaned, maxLength);
    }

    public string GetSafeDisplayName(string? name, string? email, string fallback = Constants.DefaultText.Unknown)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return TruncateText(name, Constants.TextLimits.SenderNameMaxLength);
        }
        
        if (!string.IsNullOrWhiteSpace(email))
        {
            return TruncateText(email, Constants.TextLimits.SenderNameMaxLength);
        }
        
        return fallback;
    }

    public string GetValidEmailAddress(string? email, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(email) ? fallback : email.Trim();
    }

    public string FormatEmailContent(string? content)
    {
        return CleanAndLimitContent(content);
    }

    public string GetPriorityColor(string? priority)
    {
        return priority?.ToLower() switch
        {
            "high" => Constants.Colors.HighPriority,
            "low" => Constants.Colors.LowPriority,
            _ => Constants.Colors.NormalPriority
        };
    }

    public string GetReadStatusColor(bool isRead)
    {
        return isRead ? Constants.Colors.ReadEmail : Constants.Colors.UnreadEmail;
    }

    public string GetTaskStatusColor(string? status)
    {
        return status?.ToLower() switch
        {
            "completed" => Constants.Colors.CompletedTask,
            "inprogress" => Constants.Colors.InProgressTask,
            "waitingonothers" => Constants.Colors.WaitingTask,
            "deferred" => Constants.Colors.DeferredTask,
            _ => Constants.Colors.NotStartedTask
        };
    }

    public (DateTime? Date, bool HasError, string? ErrorMessage) ParseDueDate(string? dueDate)
    {
        if (string.IsNullOrWhiteSpace(dueDate))
        {
            return (null, false, null);
        }

        // Handle natural language
        var normalized = dueDate.ToLower().Trim();
        if (normalized == "today")
        {
            return (DateTime.Today, false, null);
        }
        if (normalized == "tomorrow")
        {
            return (DateTime.Today.AddDays(1), false, null);
        }

        // Try standard formats
        var formats = new[]
        {
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dueDate, format, null, System.Globalization.DateTimeStyles.None, out var result))
            {
                return (result, false, null);
            }
        }

        if (DateTime.TryParse(dueDate, out var parsedDate))
        {
            return (parsedDate, false, null);
        }

        return (null, true, $"Invalid due date format: '{dueDate}'. Please use formats like 'tomorrow', 'next week', or 'YYYY-MM-DD'.");
    }

    public string? FormatTaskDueDate(DateTimeTimeZone? dueDateTime, string? format = null)
    {
        if (dueDateTime?.DateTime == null)
        {
            return null;
        }

        if (DateTime.TryParse(dueDateTime.DateTime, out var date))
        {
            return format switch
            {
                Constants.DateFormats.StandardDate => date.ToString("yyyy-MM-dd"),
                Constants.DateFormats.FriendlyDate => date.ToString("MMM dd, yyyy"),
                _ => date.ToString("yyyy-MM-dd")
            };
        }

        return null;
    }

    public (DateTime? startDate, DateTime? endDate) ParseTimePeriod(string? timePeriod)
    {
        if (string.IsNullOrWhiteSpace(timePeriod))
            return (null, null);

        var now = DateTime.UtcNow;
        var today = now.Date;

        return timePeriod.ToLower() switch
        {
            "today" => (today, today.AddDays(1)),
            "yesterday" => (today.AddDays(-1), today),
            "this_week" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(7 - (int)today.DayOfWeek)),
            "last_week" => (today.AddDays(-7 - (int)today.DayOfWeek), today.AddDays(-(int)today.DayOfWeek)),
            "this_month" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            "last_month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1)),
            _ when int.TryParse(timePeriod, out var days) && days > 0 => (today.AddDays(-days), today.AddDays(1)),
            _ => (null, null)
        };
    }

    public string? FormatEventDateTime(DateTimeTimeZone? dateTime, string? format = null)
    {
        if (dateTime?.DateTime == null)
        {
            return null;
        }

        if (DateTime.TryParse(dateTime.DateTime, out var date))
        {
            return format switch
            {
                Constants.DateFormats.StandardDate => date.ToString("yyyy-MM-dd"),
                Constants.DateFormats.StandardDateTime => date.ToString("yyyy-MM-ddTHH:mm:ss"),
                Constants.DateFormats.FriendlyDate => date.ToString("MMM dd, yyyy"),
                _ => date.ToString("MMM dd, yyyy HH:mm")
            };
        }

        return null;
    }

    public (DateTime? DateTime, bool HasError, string? ErrorMessage) ParseEventDateTime(string? dateTime)
    {
        if (string.IsNullOrWhiteSpace(dateTime))
        {
            return (null, true, "Date/time is required");
        }

        var normalized = dateTime.ToLower().Trim();
        
        // Handle natural language
        if (normalized.Contains("today"))
        {
            var time = ExtractTimeFromString(normalized);
            var today = DateTime.Today;
            return time.HasValue ? (today.Add(time.Value), false, null) : (today.AddHours(9), false, null); // Default to 9 AM
        }
        
        if (normalized.Contains("tomorrow"))
        {
            var time = ExtractTimeFromString(normalized);
            var tomorrow = DateTime.Today.AddDays(1);
            return time.HasValue ? (tomorrow.Add(time.Value), false, null) : (tomorrow.AddHours(9), false, null);
        }

        // Try parsing standard formats
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
            if (DateTime.TryParseExact(dateTime, format, null, System.Globalization.DateTimeStyles.None, out var result))
            {
                return (result, false, null);
            }
        }

        // Try general parsing
        if (DateTime.TryParse(dateTime, out var parsed))
        {
            return (parsed, false, null);
        }

        return (null, true, $"Invalid date/time format: '{dateTime}'. Use formats like 'tomorrow 2pm', '2024-01-15 14:00', or 'MM/dd/yyyy HH:mm'.");
    }

    private static TimeSpan? ExtractTimeFromString(string input)
    {
        // Simple time extraction - look for patterns like "2pm", "14:00", "2:30", etc.
        var timePatterns = new[]
        {
            @"(\d{1,2})(?::(\d{2}))?\s*(am|pm)",
            @"(\d{1,2}):(\d{2})",
            @"(\d{1,2})\s*(am|pm)"
        };

        foreach (var pattern in timePatterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var hour))
                {
                    var minute = match.Groups.Count > 2 && int.TryParse(match.Groups[2].Value, out var min) ? min : 0;
                    var isPm = match.Groups.Count > 3 && match.Groups[3].Value.ToLower() == "pm";
                    
                    if (isPm && hour != 12) hour += 12;
                    if (!isPm && hour == 12) hour = 0;
                    
                    if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                    {
                        return new TimeSpan(hour, minute, 0);
                    }
                }
            }
        }

        return null;
    }
} 