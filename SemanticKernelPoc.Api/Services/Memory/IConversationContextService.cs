using SemanticKernelPoc.Api.Models;

namespace SemanticKernelPoc.Api.Services.Memory;

public interface IConversationContextService
{
    /// <summary>
    /// Get conversation context for a session
    /// </summary>
    Task<ConversationContext> GetConversationContextAsync(string sessionId);

    /// <summary>
    /// Update conversation context
    /// </summary>
    Task UpdateConversationContextAsync(ConversationContext context);

    /// <summary>
    /// Clear conversation context for a session
    /// </summary>
    Task ClearConversationContextAsync(string sessionId);

    /// <summary>
    /// Get all active workflows for a user
    /// </summary>
    Task<IEnumerable<WorkflowContext>> GetActiveWorkflowsAsync(string userId);

    /// <summary>
    /// Clean up old conversation contexts
    /// </summary>
    Task CleanupOldContextsAsync(TimeSpan maxAge);
} 