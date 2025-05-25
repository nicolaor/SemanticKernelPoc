using SemanticKernelPoc.Api.Models;
using Microsoft.SemanticKernel;

namespace SemanticKernelPoc.Api.Services.Workflows;

public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Analyze user message to detect if it should trigger a workflow
    /// </summary>
    Task<WorkflowTrigger> DetectWorkflowTriggerAsync(string userMessage, ConversationContext context);

    /// <summary>
    /// Execute a workflow based on trigger and user input
    /// </summary>
    Task<WorkflowExecution> ExecuteWorkflowAsync(
        WorkflowDefinition workflow, 
        string userMessage, 
        ConversationContext context,
        Kernel kernel);

    /// <summary>
    /// Get all available workflow definitions
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetAvailableWorkflowsAsync();

    /// <summary>
    /// Get a specific workflow definition by ID
    /// </summary>
    Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId);

    /// <summary>
    /// Get workflow execution status
    /// </summary>
    Task<WorkflowExecution> GetWorkflowExecutionAsync(string executionId);

    /// <summary>
    /// Cancel a running workflow
    /// </summary>
    Task<bool> CancelWorkflowAsync(string executionId);

    /// <summary>
    /// Get predefined workflow definitions
    /// </summary>
    IEnumerable<WorkflowDefinition> GetPredefinedWorkflows();
} 