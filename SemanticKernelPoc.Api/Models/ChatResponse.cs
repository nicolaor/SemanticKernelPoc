namespace SemanticKernelPoc.Api.Models;

public class ChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsAiResponse { get; set; }
    public DateTime Timestamp { get; set; }
    
    // New structured data fields
    public CardData? Cards { get; set; }
    public ResponseMetadata? Metadata { get; set; }
}

public class CardData
{
    public string Type { get; set; } = string.Empty; // "tasks", "emails", "calendar", "capabilities"
    public object Data { get; set; } = new object();
    public int Count { get; set; }
    public string? UserName { get; set; }
    public string? TimeRange { get; set; }
}

public class ResponseMetadata
{
    public bool HasCards { get; set; }
    public string? OriginalQuery { get; set; }
    public List<string>? FunctionsUsed { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
}

// Specific card data types for better type safety
public class TaskCardData
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string? DueDateFormatted { get; set; }
    public string Created { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? MatchReason { get; set; }
    public string WebLink { get; set; } = string.Empty;
    public string PriorityColor { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
}

public class EmailCardData
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ReceivedDate { get; set; } = string.Empty;
    public DateTime? ReceivedDateTime { get; set; }
    public bool IsRead { get; set; }
    public string Importance { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string? WebLink { get; set; }
    public string? MatchReason { get; set; }
    public string ImportanceColor { get; set; } = string.Empty;
    public string ReadStatusColor { get; set; } = string.Empty;
}

public class CalendarCardData
{
    public string Subject { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public bool IsAllDay { get; set; }
    public string Id { get; set; } = string.Empty;
    public List<AttendeeData>? Attendees { get; set; }
}

public class AttendeeData
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ResponseStatus { get; set; } = string.Empty;
} 