using SemanticKernelPoc.Api.Models;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace SemanticKernelPoc.Api.Services.Workflows;

public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly ILogger<WorkflowOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, WorkflowExecution> _activeExecutions = new();
    private readonly List<WorkflowDefinition> _predefinedWorkflows;
    private readonly List<WorkflowTrigger> _triggers;

    public WorkflowOrchestrator(ILogger<WorkflowOrchestrator> logger)
    {
        _logger = logger;
        _predefinedWorkflows = InitializePredefinedWorkflows();
        _triggers = InitializeWorkflowTriggers();
    }

    public Task<WorkflowTrigger> DetectWorkflowTriggerAsync(string userMessage, ConversationContext context)
    {
        var messageLower = userMessage.ToLower();
        
        // Find matching triggers by priority
        var matchingTriggers = _triggers
            .Where(trigger => trigger.IsActive && MatchesTrigger(trigger, messageLower, context))
            .OrderByDescending(t => t.Priority)
            .ToList();

        if (matchingTriggers.Any())
        {
            var selectedTrigger = matchingTriggers.First();
            _logger.LogInformation("Detected workflow trigger: {TriggerId} for workflow: {WorkflowId}", 
                selectedTrigger.Id, selectedTrigger.WorkflowDefinitionId);
            return Task.FromResult(selectedTrigger);
        }

        return Task.FromResult(new WorkflowTrigger()); // Return empty trigger instead of null
    }

    public async Task<WorkflowExecution> ExecuteWorkflowAsync(
        WorkflowDefinition workflow, 
        string userMessage, 
        ConversationContext context,
        Kernel kernel)
    {
        var execution = new WorkflowExecution
        {
            WorkflowDefinitionId = workflow.Id,
            UserId = context.UserId,
            SessionId = context.SessionId,
            Status = WorkflowExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            TriggerMessage = userMessage,
            Context = ExtractParametersFromMessage(userMessage, workflow)
        };

        _activeExecutions[execution.Id] = execution;

        try
        {
            _logger.LogInformation("Starting workflow execution: {ExecutionId} for workflow: {WorkflowName}", 
                execution.Id, workflow.Name);

            // Execute steps in dependency order
            var sortedSteps = TopologicalSort(workflow.Steps);
            
            foreach (var step in sortedSteps)
            {
                if (execution.Status == WorkflowExecutionStatus.Failed || 
                    execution.Status == WorkflowExecutionStatus.Cancelled)
                {
                    break;
                }

                await ExecuteStepAsync(execution, step, kernel);
            }

            // Determine final status
            if (execution.Status == WorkflowExecutionStatus.Running)
            {
                var hasFailedSteps = execution.StepExecutions.Any(s => s.Status == WorkflowStepStatus.Failed);
                var hasCompletedSteps = execution.StepExecutions.Any(s => s.Status == WorkflowStepStatus.Completed);

                if (hasFailedSteps && hasCompletedSteps)
                {
                    execution.Status = WorkflowExecutionStatus.PartiallyCompleted;
                }
                else if (hasFailedSteps)
                {
                    execution.Status = WorkflowExecutionStatus.Failed;
                }
                else
                {
                    execution.Status = WorkflowExecutionStatus.Completed;
                }
            }

            execution.CompletedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Workflow execution completed: {ExecutionId} with status: {Status}", 
                execution.Id, execution.Status);
        }
        catch (Exception ex)
        {
            execution.Status = WorkflowExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;
            
            _logger.LogError(ex, "Workflow execution failed: {ExecutionId}", execution.Id);
        }

        return execution;
    }

    private async Task ExecuteStepAsync(WorkflowExecution execution, WorkflowStep step, Kernel kernel)
    {
        var stepExecution = new WorkflowStepExecution
        {
            StepId = step.Id,
            StepName = step.Name,
            Status = WorkflowStepStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        execution.StepExecutions.Add(stepExecution);

        try
        {
            // Check dependencies
            if (!await CheckDependenciesAsync(execution, step))
            {
                stepExecution.Status = WorkflowStepStatus.Skipped;
                stepExecution.CompletedAt = DateTime.UtcNow;
                return;
            }

            // Check conditions
            if (!string.IsNullOrEmpty(step.Condition.Type) && !await EvaluateConditionAsync(execution, step.Condition))
            {
                stepExecution.Status = WorkflowStepStatus.Skipped;
                stepExecution.CompletedAt = DateTime.UtcNow;
                return;
            }

            // Prepare inputs
            stepExecution.Inputs = await PrepareStepInputsAsync(execution, step);

            // Execute with retry logic
            var maxRetries = step.MaxRetries + 1; // +1 for initial attempt
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    stepExecution.RetryCount = attempt;
                    
                    var stopwatch = Stopwatch.StartNew();
                    var result = await ExecuteKernelFunctionAsync(kernel, step, stepExecution.Inputs);
                    stopwatch.Stop();
                    
                    stepExecution.ExecutionTime = stopwatch.Elapsed;
                    stepExecution.Outputs = ParseStepOutput(result);
                    stepExecution.Status = WorkflowStepStatus.Completed;
                    
                    // Apply output mappings to execution context
                    ApplyOutputMappings(execution, step, stepExecution.Outputs);
                    
                    break; // Success, exit retry loop
                }
                catch (Exception stepEx)
                {
                    if (attempt == maxRetries - 1) // Last attempt
                    {
                        stepExecution.Status = step.IsOptional ? WorkflowStepStatus.Skipped : WorkflowStepStatus.Failed;
                        stepExecution.ErrorMessage = stepEx.Message;
                        
                        if (!step.IsOptional)
                        {
                            execution.Status = WorkflowExecutionStatus.Failed;
                            execution.ErrorMessage = $"Step '{step.Name}' failed: {stepEx.Message}";
                        }
                    }
                    else
                    {
                        stepExecution.Status = WorkflowStepStatus.Retrying;
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
                    }
                }
            }
        }
        catch (Exception ex)
        {
            stepExecution.Status = WorkflowStepStatus.Failed;
            stepExecution.ErrorMessage = ex.Message;
            
            if (!step.IsOptional)
            {
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.ErrorMessage = $"Step '{step.Name}' failed: {ex.Message}";
            }
        }
        finally
        {
            stepExecution.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task<object> ExecuteKernelFunctionAsync(Kernel kernel, WorkflowStep step, Dictionary<string, object> inputs)
    {
        var function = kernel.Plugins
            .SelectMany(p => p)
            .FirstOrDefault(f => f.Name.Equals(step.FunctionName, StringComparison.OrdinalIgnoreCase) &&
                                f.PluginName?.Equals(step.PluginName, StringComparison.OrdinalIgnoreCase) == true);

        if (function == null)
        {
            throw new InvalidOperationException($"Function '{step.FunctionName}' not found in plugin '{step.PluginName}'");
        }

        var kernelArguments = new KernelArguments();
        foreach (var input in inputs)
        {
            kernelArguments[input.Key] = input.Value;
        }

        var result = await kernel.InvokeAsync(function, kernelArguments);
        return result.ToString() ?? string.Empty;
    }

    private Dictionary<string, object> ParseStepOutput(object result)
    {
        var output = new Dictionary<string, object>
        {
            ["result"] = result,
            ["success"] = true,
            ["timestamp"] = DateTime.UtcNow
        };

        // Try to parse JSON if result is a string
        if (result is string stringResult && (stringResult.StartsWith("{") || stringResult.StartsWith("[")))
        {
            try
            {
                var jsonResult = JsonSerializer.Deserialize<Dictionary<string, object>>(stringResult);
                if (jsonResult != null)
                {
                    foreach (var kvp in jsonResult)
                    {
                        output[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // Keep original string result if JSON parsing fails
            }
        }

        return output;
    }

    private void ApplyOutputMappings(WorkflowExecution execution, WorkflowStep step, Dictionary<string, object> outputs)
    {
        foreach (var mapping in step.OutputMappings)
        {
            if (outputs.TryGetValue(mapping.Key, out var value))
            {
                execution.Context[mapping.Value] = value;
            }
        }
    }

    private Task<Dictionary<string, object>> PrepareStepInputsAsync(WorkflowExecution execution, WorkflowStep step)
    {
        var inputs = new Dictionary<string, object>();

        // Add step parameters
        foreach (var param in step.Parameters)
        {
            inputs[param.Key] = param.Value;
        }

        // Replace placeholders with context values
        foreach (var key in inputs.Keys.ToList())
        {
            if (inputs[key] is string stringValue)
            {
                inputs[key] = ReplacePlaceholders(stringValue, execution.Context);
            }
        }

        return Task.FromResult(inputs);
    }

    private string ReplacePlaceholders(string input, Dictionary<string, object> context)
    {
        var pattern = @"\{\{(\w+)\}\}";
        return Regex.Replace(input, pattern, match =>
        {
            var key = match.Groups[1].Value;
            return context.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : match.Value;
        });
    }

    private Task<bool> CheckDependenciesAsync(WorkflowExecution execution, WorkflowStep step)
    {
        foreach (var dependencyId in step.DependsOn)
        {
            var dependencyExecution = execution.StepExecutions
                .FirstOrDefault(s => s.StepId == dependencyId);

            if (dependencyExecution == null || dependencyExecution.Status != WorkflowStepStatus.Completed)
            {
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(true);
    }

    private Task<bool> EvaluateConditionAsync(WorkflowExecution execution, WorkflowStepCondition condition)
    {
        if (!execution.Context.TryGetValue(condition.Field, out var fieldValue))
        {
            return Task.FromResult(false);
        }

        var result = condition.Operator.ToLower() switch
        {
            "equals" => fieldValue?.ToString() == condition.ExpectedValue?.ToString(),
            "contains" => fieldValue?.ToString()?.Contains(condition.ExpectedValue?.ToString() ?? string.Empty) == true,
            "greater" => CompareNumeric(fieldValue, condition.ExpectedValue) > 0,
            "less" => CompareNumeric(fieldValue, condition.ExpectedValue) < 0,
            _ => true
        };
        return Task.FromResult(result);
    }

    private int CompareNumeric(object value1, object value2)
    {
        if (double.TryParse(value1?.ToString(), out var num1) && 
            double.TryParse(value2?.ToString(), out var num2))
        {
            return num1.CompareTo(num2);
        }
        return 0;
    }

    private List<WorkflowStep> TopologicalSort(List<WorkflowStep> steps)
    {
        var sorted = new List<WorkflowStep>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var step in steps.OrderBy(s => s.Order))
        {
            if (!visited.Contains(step.Id))
            {
                TopologicalSortVisit(step, steps, visited, visiting, sorted);
            }
        }

        return sorted;
    }

    private void TopologicalSortVisit(WorkflowStep step, List<WorkflowStep> allSteps, 
        HashSet<string> visited, HashSet<string> visiting, List<WorkflowStep> sorted)
    {
        if (visiting.Contains(step.Id))
        {
            throw new InvalidOperationException($"Circular dependency detected involving step: {step.Name}");
        }

        if (visited.Contains(step.Id))
        {
            return;
        }

        visiting.Add(step.Id);

        foreach (var dependencyId in step.DependsOn)
        {
            var dependency = allSteps.FirstOrDefault(s => s.Id == dependencyId);
            if (dependency != null)
            {
                TopologicalSortVisit(dependency, allSteps, visited, visiting, sorted);
            }
        }

        visiting.Remove(step.Id);
        visited.Add(step.Id);
        sorted.Add(step);
    }

    private Dictionary<string, object> ExtractParametersFromMessage(string userMessage, WorkflowDefinition workflow)
    {
        var parameters = new Dictionary<string, object>(workflow.DefaultParameters);
        
        // Add user message as a parameter
        parameters["userMessage"] = userMessage;
        parameters["timestamp"] = DateTime.UtcNow;
        
        // Extract common patterns (dates, emails, names, etc.)
        ExtractCommonPatterns(userMessage, parameters);
        
        return parameters;
    }

    private void ExtractCommonPatterns(string message, Dictionary<string, object> parameters)
    {
        // Extract email addresses
        var emailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
        var emails = Regex.Matches(message, emailPattern, RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();
        if (emails.Any())
        {
            parameters["extractedEmails"] = emails;
            parameters["primaryEmail"] = emails.First();
        }

        // Extract dates (simple patterns)
        var datePatterns = new[]
        {
            @"\b\d{1,2}/\d{1,2}/\d{4}\b",
            @"\b\d{4}-\d{1,2}-\d{1,2}\b",
            @"\b(today|tomorrow|yesterday)\b"
        };

        foreach (var pattern in datePatterns)
        {
            var matches = Regex.Matches(message, pattern, RegexOptions.IgnoreCase);
            if (matches.Any())
            {
                parameters["extractedDates"] = matches.Cast<Match>().Select(m => m.Value).ToList();
                break;
            }
        }

        // Extract times
        var timePattern = @"\b\d{1,2}:\d{2}(?:\s*(?:AM|PM))?\b";
        var times = Regex.Matches(message, timePattern, RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();
        if (times.Any())
        {
            parameters["extractedTimes"] = times;
        }
    }

    private bool MatchesTrigger(WorkflowTrigger trigger, string messageLower, ConversationContext context)
    {
        return trigger.Type switch
        {
            WorkflowTriggerType.Keyword => trigger.Keywords.Any(keyword => 
                messageLower.Contains(keyword.ToLower())),
            WorkflowTriggerType.Pattern => !string.IsNullOrEmpty(trigger.Pattern) && 
                Regex.IsMatch(messageLower, trigger.Pattern, RegexOptions.IgnoreCase),
            WorkflowTriggerType.Intent => MatchesIntent(trigger, messageLower, context),
            _ => false
        };
    }

    private bool MatchesIntent(WorkflowTrigger trigger, string messageLower, ConversationContext context)
    {
        // Simple intent matching based on keywords and context
        var hasKeywords = trigger.Keywords.Any(keyword => messageLower.Contains(keyword.ToLower()));
        var contextMatches = true;

        if (trigger.Conditions.TryGetValue("workflowState", out var requiredState))
        {
            contextMatches = context.CurrentWorkflow?.CurrentState.ToString() == requiredState.ToString();
        }

        return hasKeywords && contextMatches;
    }

    public Task<IEnumerable<WorkflowDefinition>> GetAvailableWorkflowsAsync()
    {
        return Task.FromResult(_predefinedWorkflows.Where(w => w.IsActive));
    }

    public Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId)
    {
        return Task.FromResult(_predefinedWorkflows.FirstOrDefault(w => w.Id == workflowId) ?? new WorkflowDefinition());
    }

    public Task<WorkflowExecution> GetWorkflowExecutionAsync(string executionId)
    {
        return Task.FromResult(_activeExecutions.TryGetValue(executionId, out var execution) ? execution : new WorkflowExecution());
    }

    public Task<bool> CancelWorkflowAsync(string executionId)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.Status = WorkflowExecutionStatus.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public IEnumerable<WorkflowDefinition> GetPredefinedWorkflows()
    {
        return _predefinedWorkflows;
    }

    private List<WorkflowDefinition> InitializePredefinedWorkflows()
    {
        return new List<WorkflowDefinition>
        {
            CreateMeetingToTasksWorkflow(),
            CreateEmailToCalendarWorkflow(),
            CreateProjectPlanningWorkflow(),
            CreateMeetingFollowUpWorkflow(),
            CreateWeeklyReviewWorkflow()
        };
    }

    private List<WorkflowTrigger> InitializeWorkflowTriggers()
    {
        return new List<WorkflowTrigger>
        {
            new()
            {
                WorkflowDefinitionId = "meeting-to-tasks",
                Type = WorkflowTriggerType.Keyword,
                Keywords = new() { "meeting tasks", "action items from meeting", "create tasks from meeting", "meeting follow up" },
                Priority = 10
            },
            new()
            {
                WorkflowDefinitionId = "email-to-calendar",
                Type = WorkflowTriggerType.Keyword,
                Keywords = new() { "schedule from email", "create meeting from email", "email to calendar" },
                Priority = 8
            },
            new()
            {
                WorkflowDefinitionId = "project-planning",
                Type = WorkflowTriggerType.Keyword,
                Keywords = new() { "plan project", "create project plan", "project tasks", "break down project" },
                Priority = 7
            },
            new()
            {
                WorkflowDefinitionId = "meeting-follow-up",
                Type = WorkflowTriggerType.Keyword,
                Keywords = new() { "meeting follow up", "send meeting summary", "meeting recap" },
                Priority = 9
            },
            new()
            {
                WorkflowDefinitionId = "weekly-review",
                Type = WorkflowTriggerType.Keyword,
                Keywords = new() { "weekly review", "week summary", "weekly report" },
                Priority = 6
            }
        };
    }

    private WorkflowDefinition CreateMeetingToTasksWorkflow()
    {
        var workflowId = "meeting-to-tasks";
        return new WorkflowDefinition
        {
            Id = workflowId,
            Name = "Meeting to Tasks",
            Description = "Extract action items from a meeting transcript and create tasks",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = "get-transcripts",
                    Order = 1,
                    Name = "Get Meeting Transcripts",
                    Description = "Retrieve recent meeting transcripts",
                    PluginName = "MeetingPlugin",
                    FunctionName = "GetMeetingTranscripts",
                    Parameters = new() { ["count"] = 5, ["daysBack"] = 7 },
                    OutputMappings = new() { ["result"] = "meetingTranscripts" }
                },
                new()
                {
                    Id = "propose-tasks",
                    Order = 2,
                    Name = "Propose Tasks from Meeting",
                    Description = "Generate task proposals from meeting content",
                    PluginName = "MeetingPlugin",
                    FunctionName = "ProposeTasksFromMeeting",
                    Parameters = new() { ["meetingId"] = "{{selectedMeetingId}}" },
                    DependsOn = new() { "get-transcripts" },
                    OutputMappings = new() { ["result"] = "taskProposals" }
                },
                new()
                {
                    Id = "create-tasks",
                    Order = 3,
                    Name = "Create Tasks",
                    Description = "Create the proposed tasks in Microsoft To Do",
                    PluginName = "MeetingPlugin",
                    FunctionName = "CreateTasksFromProposals",
                    Parameters = new() { ["taskProposalsJson"] = "{{taskProposals}}" },
                    DependsOn = new() { "propose-tasks" },
                    OutputMappings = new() { ["result"] = "createdTasks" }
                }
            },
            DefaultParameters = new() { ["selectedMeetingId"] = "" }
        };
    }

    private WorkflowDefinition CreateEmailToCalendarWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = "email-to-calendar",
            Name = "Email to Calendar",
            Description = "Create calendar events from email content",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = "search-emails",
                    Order = 1,
                    Name = "Search Recent Emails",
                    Description = "Find recent emails that might contain meeting requests",
                    PluginName = "MailPlugin",
                    FunctionName = "GetRecentEmails",
                    Parameters = new() { ["count"] = 10, ["searchQuery"] = "meeting OR schedule OR appointment" },
                    OutputMappings = new() { ["result"] = "recentEmails" }
                },
                new()
                {
                    Id = "create-calendar-event",
                    Order = 2,
                    Name = "Create Calendar Event",
                    Description = "Create a calendar event based on email content",
                    PluginName = "CalendarPlugin",
                    FunctionName = "CreateCalendarEvent",
                    Parameters = new() 
                    { 
                        ["subject"] = "{{extractedSubject}}",
                        ["startDateTime"] = "{{extractedDateTime}}",
                        ["durationMinutes"] = 60,
                        ["attendees"] = "{{extractedAttendees}}"
                    },
                    DependsOn = new() { "search-emails" },
                    OutputMappings = new() { ["result"] = "createdEvent" }
                }
            }
        };
    }

    private WorkflowDefinition CreateProjectPlanningWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = "project-planning",
            Name = "Project Planning",
            Description = "Break down a project into tasks and schedule planning meetings",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = "create-project-note",
                    Order = 1,
                    Name = "Create Project Note",
                    Description = "Create a note with project details",
                    PluginName = "ToDoPlugin",
                    FunctionName = "CreateNote",
                    Parameters = new() 
                    { 
                        ["noteContent"] = "Project: {{userMessage}}",
                        ["details"] = "Project planning initiated on {{timestamp}}",
                        ["priority"] = "high"
                    },
                    OutputMappings = new() { ["result"] = "projectNote" }
                },
                new()
                {
                    Id = "schedule-planning-meeting",
                    Order = 2,
                    Name = "Schedule Planning Meeting",
                    Description = "Schedule a project planning meeting",
                    PluginName = "CalendarPlugin",
                    FunctionName = "FindNextAvailableSlot",
                    Parameters = new() { ["durationMinutes"] = 60 },
                    DependsOn = new() { "create-project-note" },
                    OutputMappings = new() { ["result"] = "availableSlot" }
                },
                new()
                {
                    Id = "create-planning-event",
                    Order = 3,
                    Name = "Create Planning Event",
                    Description = "Create the planning meeting event",
                    PluginName = "CalendarPlugin",
                    FunctionName = "CreateCalendarEvent",
                    Parameters = new() 
                    { 
                        ["subject"] = "Project Planning: {{userMessage}}",
                        ["startDateTime"] = "{{availableSlot}}",
                        ["durationMinutes"] = 60,
                        ["description"] = "Planning meeting for project: {{userMessage}}"
                    },
                    DependsOn = new() { "schedule-planning-meeting" },
                    OutputMappings = new() { ["result"] = "planningMeeting" }
                }
            }
        };
    }

    private WorkflowDefinition CreateMeetingFollowUpWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = "meeting-follow-up",
            Name = "Meeting Follow-up",
            Description = "Summarize meeting and send follow-up email",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = "get-meeting-transcript",
                    Order = 1,
                    Name = "Get Meeting Transcript",
                    Description = "Retrieve the meeting transcript",
                    PluginName = "MeetingPlugin",
                    FunctionName = "GetMeetingTranscript",
                    Parameters = new() { ["meetingId"] = "{{selectedMeetingId}}" },
                    OutputMappings = new() { ["result"] = "transcript" }
                },
                new()
                {
                    Id = "summarize-meeting",
                    Order = 2,
                    Name = "Summarize Meeting",
                    Description = "Create a meeting summary",
                    PluginName = "MeetingPlugin",
                    FunctionName = "SummarizeMeeting",
                    Parameters = new() { ["meetingId"] = "{{selectedMeetingId}}" },
                    DependsOn = new() { "get-meeting-transcript" },
                    OutputMappings = new() { ["result"] = "summary" }
                },
                new()
                {
                    Id = "extract-decisions",
                    Order = 3,
                    Name = "Extract Key Decisions",
                    Description = "Extract key decisions from the meeting",
                    PluginName = "MeetingPlugin",
                    FunctionName = "ExtractKeyDecisions",
                    Parameters = new() { ["meetingId"] = "{{selectedMeetingId}}" },
                    DependsOn = new() { "get-meeting-transcript" },
                    OutputMappings = new() { ["result"] = "decisions" }
                },
                new()
                {
                    Id = "send-follow-up",
                    Order = 4,
                    Name = "Send Follow-up Email",
                    Description = "Send meeting summary email",
                    PluginName = "MailPlugin",
                    FunctionName = "SendEmail",
                    Parameters = new() 
                    { 
                        ["toEmail"] = "{{attendeeEmail}}",
                        ["subject"] = "Meeting Follow-up: {{meetingSubject}}",
                        ["body"] = "Meeting Summary:\n{{summary}}\n\nKey Decisions:\n{{decisions}}",
                        ["importance"] = "normal"
                    },
                    DependsOn = new() { "summarize-meeting", "extract-decisions" },
                    OutputMappings = new() { ["result"] = "emailSent" }
                }
            }
        };
    }

    private WorkflowDefinition CreateWeeklyReviewWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = "weekly-review",
            Name = "Weekly Review",
            Description = "Generate a weekly summary of activities",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Id = "get-calendar-events",
                    Order = 1,
                    Name = "Get Calendar Events",
                    Description = "Retrieve this week's calendar events",
                    PluginName = "CalendarPlugin",
                    FunctionName = "GetCalendarEvents",
                    Parameters = new() { ["daysAhead"] = 7 },
                    OutputMappings = new() { ["result"] = "weeklyEvents" }
                },
                new()
                {
                    Id = "get-completed-tasks",
                    Order = 2,
                    Name = "Get Completed Tasks",
                    Description = "Retrieve completed tasks from this week",
                    PluginName = "ToDoPlugin",
                    FunctionName = "GetRecentNotes",
                    Parameters = new() { ["count"] = 20, ["includeCompleted"] = true },
                    OutputMappings = new() { ["result"] = "weeklyTasks" }
                },
                new()
                {
                    Id = "create-weekly-note",
                    Order = 3,
                    Name = "Create Weekly Summary",
                    Description = "Create a note with weekly summary",
                    PluginName = "ToDoPlugin",
                    FunctionName = "CreateNote",
                    Parameters = new() 
                    { 
                        ["noteContent"] = "Weekly Review - {{timestamp}}",
                        ["details"] = "Events: {{weeklyEvents}}\nTasks: {{weeklyTasks}}",
                        ["priority"] = "normal"
                    },
                    DependsOn = new() { "get-calendar-events", "get-completed-tasks" },
                    OutputMappings = new() { ["result"] = "weeklyReview" }
                }
            }
        };
    }
} 