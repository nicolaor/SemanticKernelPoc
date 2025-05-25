namespace SemanticKernelPoc.Api.Plugins.Calendar;

public record CalendarEventResponse(
    string Subject,
    string Start,
    string End,
    string Location,
    string Organizer,
    bool IsAllDay,
    string Id,
    int? AttendeeCount = null,
    string? WebLink = null
);

public record CalendarCardsData(
    string Type,
    int Count,
    string UserName,
    string TimeRange,
    IEnumerable<CalendarEventResponse> Events
);

public static class CalendarResponseFormats
{
    public static string FormatCalendarCards(CalendarCardsData data)
    {
        return $"CALENDAR_CARDS:{System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = false })}";
    }

    public static string GenerateOutlookWebLink(string eventId)
    {
        // Generate Outlook Web App link for the event
        return $"https://outlook.office.com/calendar/item/{eventId}";
    }

    public static string GenerateToDoWebLink(string taskId, string listId)
    {
        // Generate Microsoft To Do web link for the task
        return $"https://to-do.office.com/tasks/id/{taskId}/details";
    }
} 