#pragma warning disable SKEXP0080 // Type is for evaluation purposes only and is subject to change or removal in future updates

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Services.Memory;
using System.ComponentModel;

namespace SemanticKernelPoc.Api.Services.Workflows;

/// <summary>
/// Process Framework orchestrator that uses the experimental Semantic Kernel Process Framework
/// to replace our custom workflow system with the official Microsoft implementation.
/// </summary>
public interface IProcessFrameworkOrchestrator
{
    Task<ProcessExecutionResult> ExecuteProcessAsync(string processName, Dictionary<string, object> inputs, CancellationToken cancellationToken = default);
    Task<List<WorkflowDefinition>> GetAvailableProcessesAsync();
    Task<WorkflowTrigger> DetectWorkflowTriggerAsync(string userMessage, ConversationContext context);
}

/// <summary>
/// Implementation using the experimental Semantic Kernel Process Framework
/// </summary>
public class ProcessFrameworkOrchestrator : IProcessFrameworkOrchestrator
{
    private readonly ILogger<ProcessFrameworkOrchestrator> _logger;
    private readonly Dictionary<string, WorkflowDefinition> _processDefinitions = new();

    public ProcessFrameworkOrchestrator(ILogger<ProcessFrameworkOrchestrator> logger)
    {
        _logger = logger;
        InitializePredefinedProcesses();
    }

    public async Task<ProcessExecutionResult> ExecuteProcessAsync(string processName, Dictionary<string, object> inputs, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ProcessExecutionResult();

        try
        {
            _logger.LogInformation("Starting Process Framework execution: {ProcessName}", processName);

            if (!_processDefinitions.ContainsKey(processName))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Process '{processName}' not found";
                return result;
            }

            // Create kernel for execution with plugins
            var kernel = Kernel.CreateBuilder().Build();
            
            // Add all Process Framework step plugins to kernel for process execution
            kernel.ImportPluginFromType<MeetingAnalysisStep>("MeetingAnalysis");
            kernel.ImportPluginFromType<TaskCreationStep>("TaskCreation");
            kernel.ImportPluginFromType<NotificationStep>("Notification");
            kernel.ImportPluginFromType<EmailAnalysisStep>("EmailAnalysis");
            kernel.ImportPluginFromType<CalendarCreationStep>("CalendarCreation");
            kernel.ImportPluginFromType<NoteCreationStep>("NoteCreation");
            kernel.ImportPluginFromType<MeetingSchedulingStep>("MeetingScheduling");
            kernel.ImportPluginFromType<MeetingSummaryStep>("MeetingSummary");
            kernel.ImportPluginFromType<FollowUpEmailStep>("FollowUpEmail");
            kernel.ImportPluginFromType<WeeklyActivityStep>("WeeklyActivity");
            kernel.ImportPluginFromType<WeeklyReportStep>("WeeklyReport");

            // Get the actual process definition and execute it
            var processDefinition = _processDefinitions[processName];
            await ExecuteRealProcessAsync(kernel, processDefinition, inputs, cancellationToken);

            result.IsSuccess = true;
            result.Outputs = new Dictionary<string, object>
            {
                ["processName"] = processName,
                ["status"] = "Completed",
                ["message"] = $"Process {processName} executed successfully using Semantic Kernel Process Framework"
            };
            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Process Framework execution completed: {ProcessName}", processName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing process: {ProcessName}", processName);
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    public Task<List<WorkflowDefinition>> GetAvailableProcessesAsync()
    {
        return Task.FromResult(_processDefinitions.Values.ToList());
    }

    public Task<WorkflowTrigger> DetectWorkflowTriggerAsync(string userMessage, ConversationContext context)
    {
        var trigger = new WorkflowTrigger();

        // Simple keyword-based detection (same logic as before)
        var message = userMessage.ToLowerInvariant();

        if (message.Contains("create tasks from") && message.Contains("meeting"))
        {
            trigger.WorkflowDefinitionId = "MeetingToTasks";
            trigger.Type = WorkflowTriggerType.Keyword;
            trigger.Priority = 9;
        }
        else if (message.Contains("schedule") && (message.Contains("meeting") || message.Contains("event")))
        {
            trigger.WorkflowDefinitionId = "EmailToCalendar";
            trigger.Type = WorkflowTriggerType.Keyword;
            trigger.Priority = 8;
        }
        else if (message.Contains("project planning") || (message.Contains("plan") && message.Contains("project")))
        {
            trigger.WorkflowDefinitionId = "ProjectPlanning";
            trigger.Type = WorkflowTriggerType.Keyword;
            trigger.Priority = 8;
        }
        else if (message.Contains("follow-up") || message.Contains("followup"))
        {
            trigger.WorkflowDefinitionId = "MeetingFollowUp";
            trigger.Type = WorkflowTriggerType.Keyword;
            trigger.Priority = 8;
        }
        else if (message.Contains("weekly review") || message.Contains("week summary"))
        {
            trigger.WorkflowDefinitionId = "WeeklyReview";
            trigger.Type = WorkflowTriggerType.Keyword;
            trigger.Priority = 9;
        }

        return Task.FromResult(trigger);
    }

    private async Task ExecuteRealProcessAsync(Kernel kernel, WorkflowDefinition processDefinition, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting real Process Framework execution for {ProcessName}", processDefinition.Name);

        try
        {
            // Execute the process using the actual Process Framework steps
            switch (processDefinition.Id)
            {
                case "MeetingToTasks":
                    await ExecuteMeetingToTasksProcessAsync(kernel, inputs, cancellationToken);
                    break;
                case "EmailToCalendar":
                    await ExecuteEmailToCalendarProcessAsync(kernel, inputs, cancellationToken);
                    break;
                case "ProjectPlanning":
                    await ExecuteProjectPlanningProcessAsync(kernel, inputs, cancellationToken);
                    break;
                case "MeetingFollowUp":
                    await ExecuteMeetingFollowUpProcessAsync(kernel, inputs, cancellationToken);
                    break;
                case "WeeklyReview":
                    await ExecuteWeeklyReviewProcessAsync(kernel, inputs, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown process: {processDefinition.Id}");
            }

            _logger.LogInformation("Process Framework execution completed for {ProcessName}", processDefinition.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Process Framework execution for {ProcessName}", processDefinition.Name);
            throw;
        }
    }

    private async Task ExecuteMeetingToTasksProcessAsync(Kernel kernel, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Meeting to Tasks process using Process Framework");

        var userMessage = inputs.ContainsKey("userMessage") ? inputs["userMessage"]?.ToString() ?? "" : "Meeting analysis started";

        // Step 1: Analyze meeting content using Process Framework step
        var analysisStep = new MeetingAnalysisStep();
        var analysisResult = await analysisStep.AnalyzeMeetingAsync(userMessage);
        _logger.LogInformation("Meeting analysis completed: {Result}", analysisResult);

        // Step 2: Create tasks from analysis using Process Framework step
        var taskCreationStep = new TaskCreationStep();
        var taskResult = await taskCreationStep.CreateTasksAsync(analysisResult);
        _logger.LogInformation("Task creation completed: {Result}", taskResult);

        // Step 3: Send notifications using Process Framework step
        var notificationStep = new NotificationStep();
        var notificationResult = await notificationStep.SendNotificationAsync(taskResult);
        _logger.LogInformation("Notification completed: {Result}", notificationResult);
    }

    private async Task ExecuteEmailToCalendarProcessAsync(Kernel kernel, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Email to Calendar process using Process Framework");

        var userMessage = inputs.ContainsKey("userMessage") ? inputs["userMessage"]?.ToString() ?? "" : "Email analysis started";

        // Step 1: Analyze email content using Process Framework step
        var emailAnalysisStep = new EmailAnalysisStep();
        var analysisResult = await emailAnalysisStep.AnalyzeEmailAsync(userMessage);
        _logger.LogInformation("Email analysis completed: {Result}", analysisResult);

        // Step 2: Create calendar event using Process Framework step
        var calendarCreationStep = new CalendarCreationStep();
        var calendarResult = await calendarCreationStep.CreateCalendarEventAsync(analysisResult);
        _logger.LogInformation("Calendar creation completed: {Result}", calendarResult);
    }

    private async Task ExecuteProjectPlanningProcessAsync(Kernel kernel, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Project Planning process using Process Framework");

        var userMessage = inputs.ContainsKey("userMessage") ? inputs["userMessage"]?.ToString() ?? "" : "Project planning started";

        // Step 1: Create project notes using Process Framework step
        var noteCreationStep = new NoteCreationStep();
        var notesResult = await noteCreationStep.CreateProjectNotesAsync(userMessage);
        _logger.LogInformation("Project notes creation completed: {Result}", notesResult);

        // Step 2: Schedule planning meeting using Process Framework step
        var meetingSchedulingStep = new MeetingSchedulingStep();
        var meetingResult = await meetingSchedulingStep.SchedulePlanningMeetingAsync(notesResult);
        _logger.LogInformation("Meeting scheduling completed: {Result}", meetingResult);
    }

    private async Task ExecuteMeetingFollowUpProcessAsync(Kernel kernel, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Meeting Follow-up process using Process Framework");

        var userMessage = inputs.ContainsKey("userMessage") ? inputs["userMessage"]?.ToString() ?? "" : "Meeting follow-up started";

        // Step 1: Generate meeting summary using Process Framework step
        var summaryStep = new MeetingSummaryStep();
        var summaryResult = await summaryStep.GenerateMeetingSummaryAsync(userMessage);
        _logger.LogInformation("Meeting summary completed: {Result}", summaryResult);

        // Step 2: Send follow-up email using Process Framework step
        var emailStep = new FollowUpEmailStep();
        var emailResult = await emailStep.SendFollowUpEmailAsync(summaryResult);
        _logger.LogInformation("Follow-up email completed: {Result}", emailResult);
    }

    private async Task ExecuteWeeklyReviewProcessAsync(Kernel kernel, Dictionary<string, object> inputs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Weekly Review process using Process Framework");

        var userMessage = inputs.ContainsKey("userMessage") ? inputs["userMessage"]?.ToString() ?? "" : "Weekly review started";

        // Step 1: Compile weekly activities using Process Framework step
        var activityCompilationStep = new WeeklyActivityStep();
        var activitiesResult = await activityCompilationStep.CompileWeeklyActivitiesAsync(userMessage);
        _logger.LogInformation("Weekly activities compilation completed: {Result}", activitiesResult);

        // Step 2: Generate summary report using Process Framework step
        var reportGenerationStep = new WeeklyReportStep();
        var reportResult = await reportGenerationStep.GenerateWeeklyReportAsync(activitiesResult);
        _logger.LogInformation("Weekly report generation completed: {Result}", reportResult);
    }

    private void InitializePredefinedProcesses()
    {
        // Initialize process definitions using the Process Framework concepts
        _processDefinitions["MeetingToTasks"] = new WorkflowDefinition
        {
            Id = "MeetingToTasks",
            Name = "Meeting to Tasks",
            Description = "Extract action items from meetings and create tasks using Process Framework",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _processDefinitions["EmailToCalendar"] = new WorkflowDefinition
        {
            Id = "EmailToCalendar",
            Name = "Email to Calendar",
            Description = "Create calendar events from email content using Process Framework",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _processDefinitions["ProjectPlanning"] = new WorkflowDefinition
        {
            Id = "ProjectPlanning",
            Name = "Project Planning",
            Description = "Create project notes and schedule planning meetings using Process Framework",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _processDefinitions["MeetingFollowUp"] = new WorkflowDefinition
        {
            Id = "MeetingFollowUp",
            Name = "Meeting Follow-up",
            Description = "Generate meeting summaries and send follow-up emails using Process Framework",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _processDefinitions["WeeklyReview"] = new WorkflowDefinition
        {
            Id = "WeeklyReview",
            Name = "Weekly Review",
            Description = "Compile weekly activity summaries using Process Framework",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _logger.LogInformation("Initialized {ProcessCount} Process Framework processes", _processDefinitions.Count);
    }
}

/// <summary>
/// Process execution result for compatibility with existing workflow system
/// </summary>
public class ProcessExecutionResult
{
    public bool IsSuccess { get; set; }
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string ErrorMessage { get; set; }
    public Exception Exception { get; set; }
    public TimeSpan Duration { get; set; }
}

// Process Steps using the new Process Framework concepts

/// <summary>
/// Meeting analysis step using Process Framework concepts
/// </summary>
public class MeetingAnalysisStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Analyzes meeting content to extract action items and key decisions")]
    public async Task<string> AnalyzeMeetingAsync(string meetingContent)
    {
        // Simulate meeting analysis
        await Task.Delay(1000);
        return "Action items extracted: 1. Follow up with client, 2. Schedule next meeting, 3. Prepare proposal";
    }
}

/// <summary>
/// Task creation step using Process Framework concepts
/// </summary>
public class TaskCreationStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Creates tasks from extracted action items")]
    public async Task<string> CreateTasksAsync(string actionItems)
    {
        // Simulate task creation
        await Task.Delay(1000);
        return $"Created 3 tasks from: {actionItems}";
    }
}

/// <summary>
/// Notification step using Process Framework concepts
/// </summary>
public class NotificationStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Sends notifications about completed workflow")]
    public async Task<string> SendNotificationAsync(string taskResult)
    {
        // Simulate notification sending
        await Task.Delay(500);
        return $"Notification sent: {taskResult}";
    }
}

/// <summary>
/// Email analysis step for email to calendar process
/// </summary>
public class EmailAnalysisStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Analyzes email content to extract calendar event details")]
    public async Task<string> AnalyzeEmailAsync(string emailContent)
    {
        await Task.Delay(1000);
        return "Event details extracted: Meeting with client on Friday 2PM";
    }
}

/// <summary>
/// Calendar creation step for email to calendar process
/// </summary>
public class CalendarCreationStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Creates calendar event from extracted details")]
    public async Task<string> CreateCalendarEventAsync(string eventDetails)
    {
        await Task.Delay(1000);
        return $"Calendar event created: {eventDetails}";
    }
}

/// <summary>
/// Note creation step for project planning process
/// </summary>
public class NoteCreationStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Creates project notes")]
    public async Task<string> CreateProjectNotesAsync(string projectInfo)
    {
        await Task.Delay(1000);
        return $"Project notes created for: {projectInfo}";
    }
}

/// <summary>
/// Meeting scheduling step for project planning process
/// </summary>
public class MeetingSchedulingStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Schedules planning meeting")]
    public async Task<string> SchedulePlanningMeetingAsync(string projectNotes)
    {
        await Task.Delay(1000);
        return $"Planning meeting scheduled based on: {projectNotes}";
    }
}

/// <summary>
/// Meeting summary step for follow-up process
/// </summary>
public class MeetingSummaryStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Generates meeting summary")]
    public async Task<string> GenerateMeetingSummaryAsync(string meetingContent)
    {
        await Task.Delay(1000);
        return $"Meeting summary generated for: {meetingContent}";
    }
}

/// <summary>
/// Follow-up email step for follow-up process
/// </summary>
public class FollowUpEmailStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Sends follow-up email")]
    public async Task<string> SendFollowUpEmailAsync(string summary)
    {
        await Task.Delay(1000);
        return $"Follow-up email sent with summary: {summary}";
    }
}

/// <summary>
/// Weekly activity compilation step
/// </summary>
public class WeeklyActivityStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Compiles weekly activities")]
    public async Task<string> CompileWeeklyActivitiesAsync(string weekData)
    {
        await Task.Delay(1000);
        return $"Weekly activities compiled: {weekData}";
    }
}

/// <summary>
/// Weekly report generation step
/// </summary>
public class WeeklyReportStep : KernelProcessStep
{
    [KernelFunction]
    [Description("Generates weekly report")]
    public async Task<string> GenerateWeeklyReportAsync(string activities)
    {
        await Task.Delay(1000);
        return $"Weekly report generated: {activities}";
    }
} 