namespace SemanticKernelPoc.Api.Services.Shared;

/// <summary>
/// Centralized constants to replace magic numbers and hardcoded strings
/// </summary>
public static class Constants
{
    /// <summary>
    /// Text truncation lengths
    /// </summary>
    public static class TextLimits
    {
        public const int EmailSubjectMaxLength = 80;
        public const int EmailPreviewMaxLength = 120;
        public const int TaskTitleMaxLength = 80;
        public const int TaskContentMaxLength = 120;
        public const int EmailContentForAI = 500;
        public const int SearchResultContentMaxLength = 100;
        public const int SenderNameMaxLength = 50;
        public const int EventSubjectMaxLength = 80;
        public const int EventLocationMaxLength = 60;
    }

    /// <summary>
    /// Default text values
    /// </summary>
    public static class DefaultText
    {
        public const string Unknown = "Unknown";
        public const string NoSubject = "No Subject";
        public const string NoContent = "No Content";
        public const string Normal = "Normal";
        public const string NotStarted = "NotStarted";
        public const string UntitledTask = "Untitled Task";
        public const string NoLocation = "No Location";
    }

    /// <summary>
    /// Date format strings
    /// </summary>
    public static class DateFormats
    {
        public const string StandardDate = "yyyy-MM-dd";
        public const string StandardDateTime = "yyyy-MM-dd HH:mm";
        public const string FriendlyDate = "MMM dd, yyyy";
        public const string RoundTripDateTime = "O";
        public const string GraphApiDateTime = "yyyy-MM-ddTHH:mm:ss.fffK";
    }

    /// <summary>
    /// Color values for UI elements
    /// </summary>
    public static class Colors
    {
        // Priority colors
        public const string HighPriority = "#ef4444";
        public const string NormalPriority = "#6b7280";
        public const string LowPriority = "#10b981";

        // Email status colors
        public const string ReadEmail = "#6b7280";
        public const string UnreadEmail = "#3b82f6";

        // Task status colors
        public const string CompletedTask = "#10b981";
        public const string InProgressTask = "#f59e0b";
        public const string WaitingTask = "#8b5cf6";
        public const string DeferredTask = "#6b7280";
        public const string NotStartedTask = "#3b82f6";
    }

    /// <summary>
    /// Service URLs
    /// </summary>
    public static class ServiceUrls
    {
        public const string OutlookMailBase = "https://outlook.office.com/mail/id/";
        public const string ToDoTaskBase = "https://to-do.office.com/tasks/";
        public const string OutlookCalendarBase = "https://outlook.office.com/calendar/item/";

        public static string GetOutlookMailUrl(string messageId) => 
            string.IsNullOrEmpty(messageId) ? OutlookMailBase : $"{OutlookMailBase}{messageId}";

        public static string GetToDoTaskUrl(string taskId) => 
            string.IsNullOrEmpty(taskId) ? ToDoTaskBase : $"{ToDoTaskBase}{taskId}/details";

        public static string GetOutlookCalendarUrl(string eventId) => 
            string.IsNullOrEmpty(eventId) ? OutlookCalendarBase : $"{OutlookCalendarBase}{eventId}";
    }

    /// <summary>
    /// Graph API query limits
    /// </summary>
    public static class QueryLimits
    {
        public const int MaxEmailCount = 10;
        public const int MaxTaskCount = 10;
        public const int MaxTasksPerList = 50;
        public const int MaxCalendarEvents = 10;
        public const int MaxOneDriveFiles = 10;
    }

    /// <summary>
    /// Graph API select fields
    /// </summary>
    public static class GraphSelectFields
    {
        public static readonly string[] EmailBasic = 
        {
            "id", "subject", "from", "receivedDateTime", "isRead", 
            "importance", "bodyPreview", "webLink"
        };

        public static readonly string[] EmailWithBody = 
        {
            "id", "subject", "from", "receivedDateTime", "isRead", 
            "importance", "body", "bodyPreview", "webLink"
        };

        public static readonly string[] TaskBasic = 
        {
            "id", "title", "status", "importance", "createdDateTime", 
            "dueDateTime", "body"
        };
    }

    /// <summary>
    /// AI prompt templates
    /// </summary>
    public static class PromptTemplates
    {
        public const string EmailSummaryPrompt = @"Please provide a concise summary of these {0} recent emails for the user. Focus on the main topics, senders, and actionable items. Don't just list metadata - summarize what the emails are actually about:

{1}

Provide a helpful summary that tells the user what these emails are about, their priorities, and any urgent items requiring attention.";

        public const string TaskSummaryPrompt = @"Please provide a concise summary of these {0} recent tasks for the user. Focus on the main topics, priorities, deadlines, and actionable items. Don't just list metadata - summarize what the tasks are actually about:

{1}

Provide a helpful summary that tells the user what these tasks are about, their priorities, and any upcoming deadlines.";
    }

    /// <summary>
    /// Status and state colors
    /// </summary>
    public static class StatusColors
    {
        public const string Read = "#22c55e"; // Green
        public const string Unread = "#3b82f6"; // Blue
        public const string Completed = "#22c55e"; // Green
        public const string InProgress = "#f59e0b"; // Amber
        public const string NotStarted = "#6b7280"; // Gray
        public const string WaitingOnOthers = "#8b5cf6"; // Purple
        public const string Deferred = "#ef4444"; // Red
        public const string HighPriority = "#ef4444"; // Red
        public const string NormalPriority = "#6b7280"; // Gray
        public const string LowPriority = "#22c55e"; // Green
    }
} 