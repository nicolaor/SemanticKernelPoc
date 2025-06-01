using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;
using SemanticKernelPoc.Api.Plugins.ToDo;
using SemanticKernelPoc.Api.Plugins.Calendar;
using SemanticKernelPoc.Api.Plugins.Mail;
using SemanticKernelPoc.Api.Plugins.OneDrive;
using SemanticKernelPoc.Api.Plugins.SharePoint;
using SemanticKernelPoc.Api.Services;
using Microsoft.AspNetCore.Authentication;
using System.Text;

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ILogger<ChatController> logger,
    IConversationMemoryService conversationMemory,
    IResponseProcessingService responseProcessingService) : ControllerBase
{
    private readonly ILogger<ChatController> _logger = logger;
    private readonly IConversationMemoryService _conversationMemory = conversationMemory;
    private readonly IResponseProcessingService _responseProcessingService = responseProcessingService;

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostMessage([FromBody] ChatMessage chatMessage)
    {
        try
        {
            _logger.LogInformation("Received message: {Content}", chatMessage.Content);

            // Generate unique session ID for the user
            var sessionId = $"session_{chatMessage.UserId}_{DateTime.UtcNow:yyyyMMdd}";
            _logger.LogInformation("Generated SessionId {SessionId} for user {UserId}", sessionId, chatMessage.UserId);

            // Add user message to conversation memory
            chatMessage.SessionId = sessionId;
            chatMessage.IsAiResponse = false;
            chatMessage.Timestamp = DateTime.UtcNow;
            await _conversationMemory.AddMessageAsync(chatMessage);

            // Create kernel with user-specific plugins
            var kernel = await CreateUserKernelAsync(chatMessage.UserId);

            // Get conversation history for context
            var conversationHistory = await _conversationMemory.GetConversationHistoryAsync(sessionId);
            
            // Create enhanced system prompt that guides the AI to use functions naturally
            var systemPrompt = CreateEnhancedSystemPrompt(chatMessage.UserName);
            
            // Build conversation context with history
            var conversationContext = BuildConversationContext(conversationHistory.ToList(), systemPrompt, chatMessage.Content);

            // Configure execution settings for natural function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.9,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Let the AI naturally decide what functions to call and how to respond
            var result = await kernel.InvokePromptAsync(conversationContext, new KernelArguments(executionSettings));
            var aiResponse = result.GetValue<string>() ?? "I apologize, but I couldn't process your request.";

            _logger.LogInformation("=== AI RESPONSE ANALYSIS START ===");
            _logger.LogInformation("AI Response Length: {Length} characters", aiResponse.Length);
            _logger.LogInformation("AI Response Content: {Response}", aiResponse);
            
            // Log if response contains card patterns
            var cardPatterns = new[] { "TASK_CARDS:", "EMAIL_CARDS:", "CALENDAR_CARDS:", "SHAREPOINT_CARDS:" };
            var foundPatterns = cardPatterns.Where(pattern => aiResponse.Contains(pattern)).ToList();
            if (foundPatterns.Any())
            {
                _logger.LogInformation("Found card patterns: {Patterns}", string.Join(", ", foundPatterns));
            }
            else
            {
                _logger.LogInformation("No card patterns found in AI response");
            }
            
            // Log if response contains analysis patterns
            var analysisPatterns = new[] { "TASK_ANALYSIS:", "EMAIL_ANALYSIS:", "CALENDAR_ANALYSIS:" };
            var foundAnalysisPatterns = analysisPatterns.Where(pattern => aiResponse.Contains(pattern)).ToList();
            if (foundAnalysisPatterns.Any())
            {
                _logger.LogInformation("Found analysis patterns: {Patterns}", string.Join(", ", foundAnalysisPatterns));
            }
            
            _logger.LogInformation("=== AI RESPONSE ANALYSIS END ===");

            // Process the response to extract any card data
            var processedResponse = _responseProcessingService.ProcessResponse(
                aiResponse, 
                sessionId, 
                "ai-assistant", 
                "AI Assistant");

            _logger.LogInformation("=== PROCESSED RESPONSE ANALYSIS START ===");
            _logger.LogInformation("Processed Content Length: {Length} characters", processedResponse.Content?.Length ?? 0);
            _logger.LogInformation("Processed Content: {Content}", processedResponse.Content);
            _logger.LogInformation("Has Cards: {HasCards}", processedResponse.Cards != null);
            if (processedResponse.Cards != null)
            {
                _logger.LogInformation("Card Type: {Type}, Count: {Count}", processedResponse.Cards.Type, processedResponse.Cards.Count);
            }
            _logger.LogInformation("=== PROCESSED RESPONSE ANALYSIS END ===");

            // Add AI response to conversation memory
            var aiMessage = new ChatMessage
            {
                Id = processedResponse.Id,
                SessionId = sessionId,
                Content = processedResponse.Content,
                UserId = "ai-assistant",
                UserName = "AI Assistant",
                IsAiResponse = true,
                Timestamp = DateTime.UtcNow,
                Cards = processedResponse.Cards,
                Metadata = processedResponse.Metadata
            };
            await _conversationMemory.AddMessageAsync(aiMessage);

            return Ok(processedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ChatResponse
            {
                Content = "I apologize, but I encountered an error while processing your request. Please try again.",
                IsAiResponse = true,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task<Kernel> CreateUserKernelAsync(string userId)
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("SemanticKernel").Get<SemanticKernelConfig>()
            ?? throw new InvalidOperationException("SemanticKernel configuration is missing");

        var kernelBuilder = Kernel.CreateBuilder();

        // Configure AI service
        if (config.UseAzureOpenAI)
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: config.DeploymentOrModelId,
                endpoint: config.Endpoint,
                apiKey: config.ApiKey);
        }
        else
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: config.DeploymentOrModelId,
                apiKey: config.ApiKey);
        }

        // Add Microsoft Graph plugins that will use the user's token
        var userAccessToken = await HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(userAccessToken))
        {
            // Create plugin instances with dependency injection
            var graphService = HttpContext.RequestServices.GetRequiredService<IGraphService>();

            var calendarPlugin = new CalendarPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<CalendarPlugin>>());
            var todoPlugin = new ToDoPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<ToDoPlugin>>());

            // Add Microsoft Graph plugins
            kernelBuilder.Plugins.AddFromObject(new OneDrivePlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>()));
            kernelBuilder.Plugins.AddFromObject(calendarPlugin);
            kernelBuilder.Plugins.AddFromObject(new MailPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(todoPlugin);

            // Add SharePoint MCP plugin
            var mcpClientService = HttpContext.RequestServices.GetRequiredService<IMcpClientService>();
            var sharePointPlugin = new SharePointMcpPlugin(mcpClientService, HttpContext.RequestServices.GetRequiredService<ILogger<SharePointMcpPlugin>>());
            kernelBuilder.Plugins.AddFromObject(sharePointPlugin);

            _logger.LogInformation("Added OneDrive, Calendar, Mail, ToDo, and SharePoint MCP plugins for user {UserId}.", userId);
        }

        var kernel = kernelBuilder.Build();

        // Store user context in kernel for plugins to use
        if (!string.IsNullOrEmpty(userAccessToken))
        {
            kernel.Data["UserAccessToken"] = userAccessToken;
            kernel.Data["ApiUserAccessToken"] = userAccessToken;
            kernel.Data["UserId"] = userId ?? string.Empty;
            kernel.Data["UserName"] = userId ?? string.Empty;

            // Log available functions for debugging
            var functions = kernel.Plugins.GetFunctionsMetadata();
            _logger.LogInformation("Available functions for user {UserId}: {Functions}",
                userId, string.Join(", ", functions.Select(f => $"{f.PluginName}.{f.Name}")));
        }

        return kernel;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserSessions()
    {
        try
        {
            var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID not found");
            }

            var sessions = await _conversationMemory.GetUserSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user sessions");
            return StatusCode(500, "Error retrieving sessions");
        }
    }

    [HttpGet("sessions/{sessionId}/history")]
    public async Task<ActionResult<IEnumerable<ChatMessage>>> GetConversationHistory(string sessionId, [FromQuery] int maxMessages = 20)
    {
        try
        {
            var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            var history = await _conversationMemory.GetConversationHistoryAsync(sessionId, maxMessages);

            // Verify the session belongs to the user (basic security check)
            if (history.Any() && history.Any(m => !string.IsNullOrEmpty(m.UserId) && m.UserId != userId && m.UserId != "ai-assistant"))
            {
                return Forbid("Access denied to this conversation");
            }

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation history for session {SessionId}", sessionId);
            return StatusCode(500, "Error retrieving conversation history");
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult> ClearConversation(string sessionId)
    {
        try
        {
            var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            // Verify the session belongs to the user before clearing
            var history = await _conversationMemory.GetConversationHistoryAsync(sessionId, 1);
            if (history.Any() && history.Any(m => !string.IsNullOrEmpty(m.UserId) && m.UserId != userId && m.UserId != "ai-assistant"))
            {
                return Forbid("Access denied to this conversation");
            }

            await _conversationMemory.ClearConversationAsync(sessionId);
            _logger.LogInformation("Cleared conversation for session {SessionId} by user {UserId}", sessionId, userId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation for session {SessionId}", sessionId);
            return StatusCode(500, "Error clearing conversation");
        }
    }

    private string CreateEnhancedSystemPrompt(string userName)
    {
        return $@"You are an intelligent assistant helping {userName} with their Microsoft 365 tasks and data.

CRITICAL RESPONSE FORMAT RULES:

1. CARD DISPLAY vs ANALYSIS MODE:
   - For DISPLAY requests (""show"", ""list"", ""get"", ""find"", ""my tasks"", etc.): Use default function parameters to get cards
   - For ANALYSIS requests (""summarize"", ""analyze"", ""what are my tasks about"", ""overview"", etc.): Set analysisMode=true

2. CARD FORMAT PRESERVATION:
   - When functions return 'TASK_CARDS:', 'EMAIL_CARDS:', 'CALENDAR_CARDS:', or 'SHAREPOINT_CARDS:', preserve this EXACT format
   - DO NOT convert card data to natural language - keep the structured format intact
   - You can add explanatory text BEFORE the card data, but the card format must be preserved

3. ANALYSIS FORMAT HANDLING:
   - When functions return 'TASK_ANALYSIS:', 'EMAIL_ANALYSIS:', etc., provide a natural language summary
   - Extract insights, patterns, and key information from the analysis data
   - DO NOT preserve the analysis JSON format - convert it to readable text

EXAMPLES OF CORRECT RESPONSES:

Display Request:
User: 'show my tasks'
Function Call: GetRecentNotes(analysisMode=false)
Correct Response: 'Here are your recent tasks:

TASK_CARDS: [{{""id"":""task_1"",""title"":""Buy groceries"",...}}]'

Analysis Request:
User: 'summarize my tasks'
Function Call: GetRecentNotes(analysisMode=true)
Function Returns: 'TASK_ANALYSIS: [{{""title"":""Buy groceries"",""status"":""InProgress"",...}}]'
Correct Response: 'Here's a summary of your tasks:

You have 3 active tasks. Most are in progress, with 1 high-priority task due this week (Buy groceries). Your tasks focus mainly on shopping and work projects.'

FUNCTION USAGE WITH ANALYSIS MODE:
- GetRecentNotes: Use analysisMode=true for ""summarize tasks"", ""task overview"", ""what are my tasks about""
- GetRecentEmails: Use analysisMode=true for ""email summary"", ""what emails did I get""
- SearchNotes: Use analysisMode=true for ""analyze tasks about X"", ""summarize tasks containing Y""
- For display queries (""show"", ""list"", ""get""), use default analysisMode=false

Always be helpful and accurate. Pay attention to whether the user wants to see items (cards) or understand them (analysis).";
    }

    private string BuildConversationContext(List<ChatMessage> history, string systemPrompt, string currentMessage)
    {
        var context = new StringBuilder();
        context.AppendLine(systemPrompt);
        context.AppendLine();
        
        // Add recent conversation history for context
        if (history.Any())
        {
            context.AppendLine("Previous conversation:");
            foreach (var msg in history.TakeLast(5))
            {
                var role = msg.IsAiResponse ? "Assistant" : "User";
                context.AppendLine($"{role}: {msg.Content}");
            }
            context.AppendLine();
        }
        
        context.AppendLine($"User: {currentMessage}");
        context.AppendLine("Assistant:");
        
        return context.ToString();
    }
}