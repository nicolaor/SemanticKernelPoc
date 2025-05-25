using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SemanticKernelPoc.Api.Services.Intelligence;

public class SmartFunctionSelector : ISmartFunctionSelector
{
    private readonly ILogger<SmartFunctionSelector> _logger;

    // Function relevance keywords mapping
    private readonly Dictionary<string, List<string>> _functionKeywords = new()
    {
        // Calendar functions
        ["GetTodaysEvents"] = new() { "today", "today's", "schedule", "appointments", "events today", "what's today" },
        ["GetUpcomingEvents"] = new() { "upcoming", "next", "future", "coming up", "later", "appointments", "events" },
        ["GetNextAppointment"] = new() { "next appointment", "next meeting", "next event", "when is my next" },
        ["AddCalendarEvent"] = new() { "schedule", "book", "create meeting", "add event", "set up meeting", "arrange" },
        ["GetEventCount"] = new() { "how many", "count", "this week", "this month", "events in" },
        
        // Note/Task functions
        ["GetRecentNotes"] = new() { "notes", "my notes", "show notes", "recent notes", "note list", "tasks", "my tasks", "todo", "to do", "assigned", "assigned to me", "task list", "what tasks", "show tasks", "recent tasks", "my todo", "todo list" },
        ["CreateNote"] = new() { "create note", "add note", "make note", "note down", "remember", "jot down", "create task", "add task", "new task" },
        ["SearchNotes"] = new() { "find note", "search notes", "look for note", "note about", "find task", "search tasks", "task about", "look for task" },
        
        // Email functions
        ["GetRecentEmails"] = new() { "emails", "mail", "messages", "inbox", "recent emails" },
        ["SendEmail"] = new() { "send email", "email", "send message", "write email", "compose" },
        ["SearchEmails"] = new() { "find email", "search email", "email from", "email about" },
        
        // Meeting functions
        ["GetMeetingTranscripts"] = new() { "transcript", "meeting notes", "recording", "what was discussed" },
        ["ProposeTasksFromMeeting"] = new() { "tasks from meeting", "action items", "follow up", "meeting tasks" },
        
        // File functions
        ["GetOneDriveFiles"] = new() { "files", "documents", "onedrive", "my files" },
        ["GetUserSites"] = new() { "sharepoint", "sites", "team sites", "collaboration" }
    };

    // Workflow state keywords
    private readonly Dictionary<WorkflowState, List<string>> _workflowKeywords = new()
    {
        [WorkflowState.SchedulingMeeting] = new() { "schedule", "meeting", "book", "arrange", "set up", "calendar" },
        [WorkflowState.CreatingNote] = new() { "note", "remember", "jot down", "write down", "create note" },
        [WorkflowState.SearchingEmails] = new() { "email", "find", "search", "look for", "inbox" },
        [WorkflowState.ProcessingMeetingTranscript] = new() { "transcript", "meeting", "summary", "decisions" },
        [WorkflowState.CreatingTasks] = new() { "task", "todo", "action item", "follow up", "assigned", "assigned to me", "my tasks", "task list", "create task" },
        [WorkflowState.SendingEmail] = new() { "send", "email", "message", "compose", "write" }
    };

    public SmartFunctionSelector(ILogger<SmartFunctionSelector> logger)
    {
        _logger = logger;
    }

    public SmartFunctionSelection SelectRelevantFunctions(
        string userMessage,
        ConversationContext conversationContext,
        IEnumerable<KernelFunctionMetadata> availableFunctions)
    {
        var stopwatch = Stopwatch.StartNew();
        var scores = new List<FunctionRelevanceScore>();
        
        var messageLower = userMessage.ToLowerInvariant();
        
        foreach (var function in availableFunctions)
        {
            var score = CalculateFunctionRelevance(messageLower, function, conversationContext);
            scores.Add(score);
        }

        // Sort by score and select top functions
        var topFunctions = scores
            .Where(s => s.Score > 0.1) // Minimum relevance threshold
            .OrderByDescending(s => s.Score)
            .Take(8) // Limit to top 8 functions to manage token usage
            .ToList();

        var selectedFunctionNames = topFunctions.Select(f => $"{f.PluginName}.{f.FunctionName}").ToList();

        stopwatch.Stop();

        var selection = new SmartFunctionSelection
        {
            SelectedFunctions = selectedFunctionNames,
            Scores = topFunctions,
            SelectionReason = GenerateSelectionReason(topFunctions, conversationContext),
            SelectionTime = stopwatch.Elapsed
        };

        _logger.LogInformation("Selected {Count} functions in {Time}ms for message: '{Message}'", 
            selectedFunctionNames.Count, stopwatch.ElapsedMilliseconds, userMessage.Substring(0, Math.Min(50, userMessage.Length)));

        return selection;
    }

    public WorkflowState PredictWorkflowState(string userMessage, ConversationContext context)
    {
        var messageLower = userMessage.ToLowerInvariant();
        var scores = new Dictionary<WorkflowState, double>();

        foreach (var (workflowState, keywords) in _workflowKeywords)
        {
            var score = keywords.Sum(keyword => 
                messageLower.Contains(keyword) ? 1.0 + (keyword.Length * 0.1) : 0.0);
            
            // Boost score if we're already in a related workflow
            if (context.CurrentWorkflow.CurrentState == workflowState)
            {
                score *= 1.5;
            }

            scores[workflowState] = score;
        }

        var predictedState = scores.OrderByDescending(s => s.Value).FirstOrDefault();
        
        _logger.LogDebug("Predicted workflow state: {State} (score: {Score}) for message: '{Message}'", 
            predictedState.Key, predictedState.Value, userMessage.Substring(0, Math.Min(30, userMessage.Length)));

        return predictedState.Value > 0.5 ? predictedState.Key : WorkflowState.None;
    }

    public void UpdateConversationContext(
        ConversationContext context,
        string userMessage,
        string aiResponse,
        IEnumerable<string> calledFunctions)
    {
        context.LastActivity = DateTime.UtcNow;
        
        // Update recent topics (extract key terms)
        var topics = ExtractTopics(userMessage);
        foreach (var topic in topics)
        {
            if (!context.RecentTopics.Contains(topic))
            {
                context.RecentTopics.Add(topic);
            }
        }
        
        // Keep only recent topics (last 10)
        if (context.RecentTopics.Count > 10)
        {
            context.RecentTopics = context.RecentTopics.TakeLast(10).ToList();
        }

        // Update function usage
        foreach (var functionCall in calledFunctions)
        {
            context.RecentFunctionCalls.Add(functionCall);
            context.FunctionUsageCount[functionCall] = context.FunctionUsageCount.GetValueOrDefault(functionCall, 0) + 1;
        }

        // Keep only recent function calls (last 20)
        if (context.RecentFunctionCalls.Count > 20)
        {
            context.RecentFunctionCalls = context.RecentFunctionCalls.TakeLast(20).ToList();
        }

        // Update workflow state if needed
        var predictedState = PredictWorkflowState(userMessage, context);
        if (predictedState != WorkflowState.None)
        {
            if (context.CurrentWorkflow.CurrentState != predictedState)
            {
                // Starting new workflow
                context.CurrentWorkflow = new WorkflowContext
                {
                    CurrentState = predictedState,
                    CurrentWorkflowId = Guid.NewGuid().ToString(),
                    LastActivity = DateTime.UtcNow,
                    StepNumber = 1
                };
            }
            else
            {
                // Continuing existing workflow
                context.CurrentWorkflow.StepNumber++;
                context.CurrentWorkflow.LastActivity = DateTime.UtcNow;
            }
        }

        _logger.LogDebug("Updated conversation context for session {SessionId}: {TopicCount} topics, {FunctionCount} recent functions, workflow: {WorkflowState}", 
            context.SessionId, context.RecentTopics.Count, context.RecentFunctionCalls.Count, context.CurrentWorkflow.CurrentState);
    }

    private FunctionRelevanceScore CalculateFunctionRelevance(
        string messageLower,
        KernelFunctionMetadata function,
        ConversationContext context)
    {
        double score = 0.0;
        var reasons = new List<string>();

        var functionKey = function.Name;
        
        // 1. Keyword matching
        if (_functionKeywords.TryGetValue(functionKey, out var keywords))
        {
            var keywordScore = keywords.Sum(keyword => 
                messageLower.Contains(keyword) ? 1.0 + (keyword.Length * 0.1) : 0.0);
            
            if (keywordScore > 0)
            {
                score += keywordScore;
                reasons.Add($"keyword match ({keywordScore:F1})");
            }
        }

        // 2. Recent usage boost
        var recentUsage = context.RecentFunctionCalls.Count(f => f.Contains(function.Name));
        if (recentUsage > 0)
        {
            var usageBoost = Math.Min(recentUsage * 0.2, 0.8);
            score += usageBoost;
            reasons.Add($"recent usage ({usageBoost:F1})");
        }

        // 3. Workflow context boost
        var workflowBoost = GetWorkflowContextBoost(function.Name, context.CurrentWorkflow.CurrentState);
        if (workflowBoost > 0)
        {
            score += workflowBoost;
            reasons.Add($"workflow context ({workflowBoost:F1})");
        }

        // 4. Function description matching (simple word overlap)
        var descriptionWords = function.Description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var messageWords = messageLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = descriptionWords.Intersect(messageWords).Count();
        if (overlap > 0)
        {
            var descriptionBoost = overlap * 0.1;
            score += descriptionBoost;
            reasons.Add($"description match ({descriptionBoost:F1})");
        }

        return new FunctionRelevanceScore
        {
            FunctionName = function.Name,
            PluginName = function.PluginName,
            Score = score,
            Reason = string.Join(", ", reasons)
        };
    }

    private double GetWorkflowContextBoost(string functionName, WorkflowState currentState)
    {
        return currentState switch
        {
            WorkflowState.SchedulingMeeting when functionName.Contains("Calendar") || functionName.Contains("Event") => 0.5,
            WorkflowState.CreatingNote when functionName.Contains("Note") || functionName.Contains("Task") => 0.5,
            WorkflowState.SearchingEmails when functionName.Contains("Email") || functionName.Contains("Mail") => 0.5,
            WorkflowState.ProcessingMeetingTranscript when functionName.Contains("Meeting") || functionName.Contains("Transcript") => 0.5,
            WorkflowState.SendingEmail when functionName.Contains("Send") || functionName.Contains("Email") => 0.5,
            _ => 0.0
        };
    }

    private List<string> ExtractTopics(string message)
    {
        // Simple topic extraction - in production, you might use NLP libraries
        var words = Regex.Split(message.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 3 && !IsStopWord(w))
            .Distinct()
            .Take(5)
            .ToList();

        return words;
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> { "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see", "two", "who", "boy", "did", "she", "use", "her", "now", "air", "any", "may", "say" };
        return stopWords.Contains(word);
    }

    private string GenerateSelectionReason(List<FunctionRelevanceScore> topFunctions, ConversationContext context)
    {
        if (!topFunctions.Any())
            return "No relevant functions found";

        var topFunction = topFunctions.First();
        var reason = $"Selected {topFunctions.Count} functions. Top match: {topFunction.FunctionName} ({topFunction.Score:F1})";
        
        if (context.CurrentWorkflow.CurrentState != WorkflowState.None)
        {
            reason += $" | Workflow: {context.CurrentWorkflow.CurrentState}";
        }

        return reason;
    }
} 