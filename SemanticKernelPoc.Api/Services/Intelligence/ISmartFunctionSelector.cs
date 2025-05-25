using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Models;

namespace SemanticKernelPoc.Api.Services.Intelligence;

public interface ISmartFunctionSelector
{
    /// <summary>
    /// Select the most relevant functions based on user message and conversation context
    /// </summary>
    SmartFunctionSelection SelectRelevantFunctions(
        string userMessage,
        ConversationContext conversationContext,
        IEnumerable<KernelFunctionMetadata> availableFunctions);

    /// <summary>
    /// Analyze user message to determine likely workflow intent
    /// </summary>
    WorkflowState PredictWorkflowState(string userMessage, ConversationContext context);

    /// <summary>
    /// Update conversation context based on user message and AI response
    /// </summary>
    void UpdateConversationContext(
        ConversationContext context,
        string userMessage,
        string aiResponse,
        IEnumerable<string> calledFunctions);
} 