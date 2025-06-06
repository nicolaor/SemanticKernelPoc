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
    string WebLink = "",
    List<AttendeeInfo> Attendees = null
);

public record AttendeeInfo(
    string Name,
    string Email,
    string ResponseStatus = "None"
);

public record CalendarCardsData(
    string Type,
    int Count,
    string UserName,
    string TimeRange,
    IEnumerable<CalendarEventResponse> Events
);

public static class CalendarLinkHelpers
{
    public static string GenerateOutlookWebLink(string eventId)
    {
        // Generate Outlook Web App link for the event using the correct format
        // URL-encode the event ID to handle special characters
        var encodedEventId = System.Web.HttpUtility.UrlEncode(eventId);
        return $"https://outlook.office365.com/owa/?itemid={encodedEventId}&exvsurl=1&path=/calendar/item";
    }

    public static string GenerateToDoWebLink(string taskId, string listId)
    {
        // Generate Microsoft To Do web link for the task
        return $"https://to-do.office.com/tasks/id/{taskId}/details";
    }
}