using SemanticKernelPoc.Api.Models;
using System.Collections.Concurrent;

namespace SemanticKernelPoc.Api.Services.Memory;

public class InMemoryConversationService(ILogger<InMemoryConversationService> logger) : IConversationMemoryService
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<InMemoryConversationService> _logger = logger;

    public Task<IEnumerable<ChatMessage>> GetConversationHistoryAsync(string sessionId, int maxMessages = 20)
    {
        _lock.EnterReadLock();
        try
        {
            if (_conversations.TryGetValue(sessionId, out var messages))
            {
                var recentMessages = messages
                    .OrderBy(m => m.Timestamp)
                    .TakeLast(maxMessages)
                    .ToList();

                _logger.LogInformation("Retrieved {Count} messages for session {SessionId}", recentMessages.Count, sessionId);
                return Task.FromResult<IEnumerable<ChatMessage>>(recentMessages);
            }

            _logger.LogInformation("No conversation history found for session {SessionId}", sessionId);
            return Task.FromResult<IEnumerable<ChatMessage>>([]);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task AddMessageAsync(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.SessionId))
        {
            throw new ArgumentException("Message must have a SessionId", nameof(message));
        }

        _lock.EnterWriteLock();
        try
        {
            // Add message to conversation
            _conversations.AddOrUpdate(
                message.SessionId,
                [message],
                (key, existingMessages) =>
                {
                    existingMessages.Add(message);
                    return existingMessages;
                });

            // Track user sessions
            if (!string.IsNullOrEmpty(message.UserId))
            {
                _userSessions.AddOrUpdate(
                    message.UserId,
                    [message.SessionId],
                    (key, existingSessions) =>
                    {
                        existingSessions.Add(message.SessionId);
                        return existingSessions;
                    });
            }

            _logger.LogInformation("Added message to session {SessionId} for user {UserId}",
                message.SessionId, message.UserId);

            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ClearConversationAsync(string sessionId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_conversations.TryRemove(sessionId, out var removedMessages))
            {
                // Also remove from user sessions
                var userId = removedMessages.FirstOrDefault()?.UserId;
                if (!string.IsNullOrEmpty(userId) && _userSessions.TryGetValue(userId, out var sessions))
                {
                    sessions.Remove(sessionId);
                    if (!sessions.Any())
                    {
                        _userSessions.TryRemove(userId, out _);
                    }
                }

                _logger.LogInformation("Cleared conversation for session {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<IEnumerable<string>> GetUserSessionsAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            if (_userSessions.TryGetValue(userId, out var sessions))
            {
                return Task.FromResult<IEnumerable<string>>([.. sessions]);
            }

            return Task.FromResult<IEnumerable<string>>([]);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task CleanupOldConversationsAsync(TimeSpan maxAge)
    {
        _lock.EnterWriteLock();
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(maxAge);
            var sessionsToRemove = new List<string>();

            foreach (var (sessionId, messages) in _conversations)
            {
                var lastMessageTime = messages.LastOrDefault()?.Timestamp ?? DateTime.MinValue;
                if (lastMessageTime < cutoffTime)
                {
                    sessionsToRemove.Add(sessionId);
                }
            }

            foreach (var sessionId in sessionsToRemove)
            {
                _conversations.TryRemove(sessionId, out var removedMessages);

                // Also clean up user sessions
                var userId = removedMessages?.FirstOrDefault()?.UserId;
                if (!string.IsNullOrEmpty(userId) && _userSessions.TryGetValue(userId, out var sessions))
                {
                    sessions.Remove(sessionId);
                    if (!sessions.Any())
                    {
                        _userSessions.TryRemove(userId, out _);
                    }
                }
            }

            _logger.LogInformation("Cleaned up {Count} old conversations older than {MaxAge}",
                sessionsToRemove.Count, maxAge);

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