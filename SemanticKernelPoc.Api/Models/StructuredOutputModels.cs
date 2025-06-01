using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SemanticKernelPoc.Api.Models;

/// <summary>
/// Base class for all structured AI responses
/// </summary>
public abstract class StructuredAIResponse
{
    [Description("The type of response (task, email, calendar, analysis, info, error)")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [Description("A brief human-readable summary of the response")]
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [Description("Whether the response contains structured data that should be displayed as cards")]
    [JsonPropertyName("hasCards")]
    public bool HasCards { get; set; }

    [Description("Confidence level of the AI response (0.0 to 1.0)")]
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Response for task-related queries
/// </summary>
public class StructuredTaskResponse : StructuredAIResponse
{
    [Description("List of tasks when the user wants to see task cards")]
    [JsonPropertyName("tasks")]
    public List<TaskCardData>? Tasks { get; set; }

    [Description("Number of tasks found")]
    [JsonPropertyName("taskCount")]
    public int TaskCount { get; set; }

    [Description("Human-readable message about the tasks")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [Description("The action performed (list, create, update, search)")]
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    public StructuredTaskResponse()
    {
        Type = "task";
    }
}

/// <summary>
/// Response for email-related queries
/// </summary>
public class StructuredEmailResponse : StructuredAIResponse
{
    [Description("List of emails when the user wants to see email cards")]
    [JsonPropertyName("emails")]
    public List<EmailCardData>? Emails { get; set; }

    [Description("Number of emails found")]
    [JsonPropertyName("emailCount")]
    public int EmailCount { get; set; }

    [Description("Human-readable message about the emails")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [Description("The action performed (list, search, create, send)")]
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    public StructuredEmailResponse()
    {
        Type = "email";
    }
}

/// <summary>
/// Response for calendar-related queries
/// </summary>
public class StructuredCalendarResponse : StructuredAIResponse
{
    [Description("List of calendar events when the user wants to see calendar cards")]
    [JsonPropertyName("events")]
    public List<CalendarCardData>? Events { get; set; }

    [Description("Number of events found")]
    [JsonPropertyName("eventCount")]
    public int EventCount { get; set; }

    [Description("Human-readable message about the calendar events")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [Description("The action performed (list, create, update, search)")]
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    public StructuredCalendarResponse()
    {
        Type = "calendar";
    }
}

/// <summary>
/// Response for informational queries that don't need cards
/// </summary>
public class InfoResponse : StructuredAIResponse
{
    [Description("The main information content to display to the user")]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [Description("Additional details if needed")]
    [JsonPropertyName("details")]
    public Dictionary<string, string>? Details { get; set; }

    [Description("Whether this is a success or informational message")]
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; } = true;

    public InfoResponse()
    {
        Type = "info";
        HasCards = false;
    }
}

/// <summary>
/// Response for analysis and summary queries
/// </summary>
public class AnalysisResponse : StructuredAIResponse
{
    [Description("The analysis or summary text")]
    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    [Description("Key insights from the analysis")]
    [JsonPropertyName("insights")]
    public List<string>? Insights { get; set; }

    [Description("Numerical statistics if applicable")]
    [JsonPropertyName("statistics")]
    public Dictionary<string, object>? Statistics { get; set; }

    [Description("Recommendations based on the analysis")]
    [JsonPropertyName("recommendations")]
    public List<string>? Recommendations { get; set; }

    public AnalysisResponse()
    {
        Type = "analysis";
        HasCards = false;
    }
}

/// <summary>
/// Response for error conditions
/// </summary>
public class ErrorResponse : StructuredAIResponse
{
    [Description("The error message to display to the user")]
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [Description("Suggestions for how the user can resolve or work around the error")]
    [JsonPropertyName("suggestions")]
    public List<string>? Suggestions { get; set; }

    [Description("Whether the error is recoverable")]
    [JsonPropertyName("isRecoverable")]
    public bool IsRecoverable { get; set; } = true;

    public ErrorResponse()
    {
        Type = "error";
        HasCards = false;
        Confidence = 1.0;
    }
}

/// <summary>
/// Intent classification for better structured responses
/// </summary>
public class UserIntent
{
    [Description("The primary intent: list, search, create, update, delete, analyze, help")]
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = string.Empty;

    [Description("The data type: task, email, calendar, file, general")]
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;

    [Description("Confidence in the intent classification")]
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;

    [Description("Key parameters extracted from the user query")]
    [JsonPropertyName("parameters")]
    public Dictionary<string, string>? Parameters { get; set; }

    [Description("Whether the user wants to see detailed cards or just a summary")]
    [JsonPropertyName("wantsCards")]
    public bool WantsCards { get; set; } = true;
} 