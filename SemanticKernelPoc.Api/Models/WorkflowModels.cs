namespace SemanticKernelPoc.Api.Models;

public enum WorkflowState
{
    None,
    SchedulingMeeting,
    CreatingNote,
    SearchingEmails,
    ProcessingMeetingTranscript,
    CreatingTasks,
    BrowsingFiles,
    SendingEmail
}

public class WorkflowContext
{
    public WorkflowState CurrentState { get; set; } = WorkflowState.None;
    public Dictionary<string, object> CollectedData { get; set; } = new();
    public List<string> PendingQuestions { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public string CurrentWorkflowId { get; set; } = string.Empty;
    public int StepNumber { get; set; } = 0;
    public bool IsComplete { get; set; } = false;
}

public class ConversationContext
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public WorkflowContext CurrentWorkflow { get; set; } = new();
    public Dictionary<string, object> UserPreferences { get; set; } = new();
    public List<string> RecentTopics { get; set; } = new();
    public List<string> RecentFunctionCalls { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> FunctionUsageCount { get; set; } = new();
}

public class FunctionRelevanceScore
{
    public string FunctionName { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SmartFunctionSelection
{
    public List<string> SelectedFunctions { get; set; } = new();
    public List<FunctionRelevanceScore> Scores { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
    public TimeSpan SelectionTime { get; set; }
}

public class WorkflowDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> DependsOn { get; set; } = new(); // Step IDs this step depends on
    public Dictionary<string, string> OutputMappings { get; set; } = new(); // Map outputs to next step inputs
    public WorkflowStepCondition Condition { get; set; } = new();
    public bool IsOptional { get; set; } = false;
    public int MaxRetries { get; set; } = 0;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

public class WorkflowStepCondition
{
    public string Type { get; set; } = string.Empty; // "success", "contains", "equals", "custom"
    public string Field { get; set; } = string.Empty; // Field to check from previous step
    public object ExpectedValue { get; set; } = new();
    public string Operator { get; set; } = "equals"; // "equals", "contains", "greater", "less"
}

public class WorkflowExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowDefinitionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.NotStarted;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<WorkflowStepExecution> StepExecutions { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new(); // Shared data between steps
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> FinalOutputs { get; set; } = new();
    public string TriggerMessage { get; set; } = string.Empty;
}

public class WorkflowStepExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Inputs { get; set; } = new();
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
    public TimeSpan? ExecutionTime { get; set; }
}

public enum WorkflowExecutionStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Cancelled,
    PartiallyCompleted
}

public enum WorkflowStepStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Skipped,
    Retrying
}

public class WorkflowTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowDefinitionId { get; set; } = string.Empty;
    public WorkflowTriggerType Type { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string Pattern { get; set; } = string.Empty;
    public Dictionary<string, object> Conditions { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0; // Higher number = higher priority
}

public enum WorkflowTriggerType
{
    Keyword,
    Pattern,
    Intent,
    Schedule,
    Event
} 