using SemanticKernelPoc.Api.Models;

namespace SemanticKernelPoc.Api.Services.Memory;

public interface IConversationMemoryService
{
    /// <summary>
    /// Get conversation history for a specific session
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetConversationHistoryAsync(string sessionId, int maxMessages = 20);

    /// <summary>
    /// Add a message to the conversation history
    /// </summary>
    Task AddMessageAsync(ChatMessage message);

    /// <summary>
    /// Clear conversation history for a session
    /// </summary>
    Task ClearConversationAsync(string sessionId);

    /// <summary>
    /// Get all sessions for a user
    /// </summary>
    Task<IEnumerable<string>> GetUserSessionsAsync(string userId);

    /// <summary>
    /// Clean up old conversations (implementation specific)
    /// </summary>
    Task CleanupOldConversationsAsync(TimeSpan maxAge);
} 