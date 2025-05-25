using SemanticKernelPoc.Api.Models;
using System.Collections.Concurrent;

namespace SemanticKernelPoc.Api.Services.Memory;

public class InMemoryConversationContextService : IConversationContextService
{
    private readonly ConcurrentDictionary<string, ConversationContext> _contexts = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<InMemoryConversationContextService> _logger;

    public InMemoryConversationContextService(ILogger<InMemoryConversationContextService> logger)
    {
        _logger = logger;
    }

    public Task<ConversationContext> GetConversationContextAsync(string sessionId)
    {
        _lock.EnterReadLock();
        try
        {
            if (_contexts.TryGetValue(sessionId, out var context))
            {
                _logger.LogDebug("Retrieved conversation context for session {SessionId}", sessionId);
                return Task.FromResult(context);
            }

            // Create new context if none exists
            var newContext = new ConversationContext
            {
                SessionId = sessionId,
                LastActivity = DateTime.UtcNow
            };

            _contexts.TryAdd(sessionId, newContext);
            _logger.LogDebug("Created new conversation context for session {SessionId}", sessionId);
            
            return Task.FromResult(newContext);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task UpdateConversationContextAsync(ConversationContext context)
    {
        _lock.EnterWriteLock();
        try
        {
            context.LastActivity = DateTime.UtcNow;
            _contexts.AddOrUpdate(context.SessionId, context, (key, existing) => context);
            
            _logger.LogDebug("Updated conversation context for session {SessionId}: workflow {WorkflowState}, {TopicCount} topics", 
                context.SessionId, context.CurrentWorkflow.CurrentState, context.RecentTopics.Count);
            
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ClearConversationContextAsync(string sessionId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_contexts.TryRemove(sessionId, out var removedContext))
            {
                _logger.LogInformation("Cleared conversation context for session {SessionId}", sessionId);
            }
            
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<IEnumerable<WorkflowContext>> GetActiveWorkflowsAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var activeWorkflows = _contexts.Values
                .Where(c => c.UserId == userId && 
                           c.CurrentWorkflow.CurrentState != WorkflowState.None && 
                           !c.CurrentWorkflow.IsComplete)
                .Select(c => c.CurrentWorkflow)
                .ToList();

            _logger.LogDebug("Found {Count} active workflows for user {UserId}", activeWorkflows.Count, userId);
            
            return Task.FromResult<IEnumerable<WorkflowContext>>(activeWorkflows);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task CleanupOldContextsAsync(TimeSpan maxAge)
    {
        _lock.EnterWriteLock();
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(maxAge);
            var contextsToRemove = new List<string>();

            foreach (var (sessionId, context) in _contexts)
            {
                if (context.LastActivity < cutoffTime)
                {
                    contextsToRemove.Add(sessionId);
                }
            }

            foreach (var sessionId in contextsToRemove)
            {
                _contexts.TryRemove(sessionId, out _);
            }

            _logger.LogInformation("Cleaned up {Count} old conversation contexts older than {MaxAge}", 
                contextsToRemove.Count, maxAge);

            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
} 