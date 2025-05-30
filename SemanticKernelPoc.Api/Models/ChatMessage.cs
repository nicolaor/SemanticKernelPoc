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
    
    // New structured data fields
    public CardData? Cards { get; set; }
    public ResponseMetadata? Metadata { get; set; }
}