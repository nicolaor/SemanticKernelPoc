namespace SemanticKernelPoc.Api.Models;

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsAiResponse { get; set; } = false;
    public string SessionId { get; set; } = string.Empty;
    public WorkflowState? WorkflowState { get; set; }
    public string WorkflowId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
} 