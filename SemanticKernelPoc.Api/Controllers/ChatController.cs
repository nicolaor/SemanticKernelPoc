using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Plugins.Calendar;
using SemanticKernelPoc.Api.Plugins.SharePoint;
using SemanticKernelPoc.Api.Plugins.OneDrive;
using SemanticKernelPoc.Api.Plugins.Mail;
using SemanticKernelPoc.Api.Plugins.ToDo;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;
using SemanticKernelPoc.Api.Services;
using System.Text;
using Microsoft.AspNetCore.Authentication;

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ILogger<ChatController> logger,
    Kernel kernel,
    IConversationMemoryService conversationMemory,
    IResponseProcessingService responseProcessingService) : ControllerBase
{
    private readonly ILogger<ChatController> _logger = logger;
    private readonly Kernel _kernel = kernel;
    private readonly IConversationMemoryService _conversationMemory = conversationMemory;
    private readonly IResponseProcessingService _responseProcessingService = responseProcessingService;

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatMessage message)
    {
        try
        {
            var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            var userName = User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value ?? "User";

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID not found");
            }

            // Get user access token from the current HTTP context
            var userAccessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(userAccessToken))
            {
                return BadRequest("User access token not found");
            }

            // Ensure message has a SessionId - generate one if not provided
            if (string.IsNullOrEmpty(message.SessionId))
            {
                message.SessionId = $"session_{userId}_{DateTime.UtcNow:yyyyMMdd}";
                _logger.LogInformation("Generated SessionId {SessionId} for user {UserId}", message.SessionId, userId);
            }

            // Store user message in conversation history
            message.UserId = userId;
            message.UserName = userName;
            message.IsAiResponse = false;
            message.Timestamp = DateTime.UtcNow;
            await _conversationMemory.AddMessageAsync(message);

            // Create user-specific kernel with their access token
            var userKernel = await CreateUserContextKernelAsync(userAccessToken, userId, userName);

            // Get conversation history for context
            var conversationHistory = await _conversationMemory.GetConversationHistoryAsync(message.SessionId, 10);

            // Build context for the AI with updated instructions for structured responses
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("You are an AI assistant that helps users manage their Microsoft 365 data including emails, calendar events, tasks, and SharePoint content.");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("RESPONSE FORMAT RULES:");
            contextBuilder.AppendLine("When functions return structured data (starting with 'EMAIL_CARDS:', 'TASK_CARDS:', or 'CALENDAR_CARDS:'), follow these rules:");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("1. FOR LISTING/BROWSING REQUESTS:");
            contextBuilder.AppendLine("   - User asks to 'list', 'show', 'get', 'display', 'search', 'browse', or wants to see specific items");
            contextBuilder.AppendLine("   - This includes: 'my tasks', 'my emails', 'my appointments', 'next appointment', 'last appointment', 'upcoming events'");
            contextBuilder.AppendLine("   - Return ONLY the raw structured data exactly as provided by the function");
            contextBuilder.AppendLine("   - Do NOT add any explanatory text, summaries, or formatting");
            contextBuilder.AppendLine("   - Do NOT modify, reformat, or change the function output in any way");
            contextBuilder.AppendLine("   - Copy and paste the function response character-for-character");
            contextBuilder.AppendLine("   - Example: If function returns 'TASK_CARDS: [{...}]', respond with exactly 'TASK_CARDS: [{...}]'");
            contextBuilder.AppendLine("   - Example: If function returns 'CALENDAR_CARDS: [{...}]', respond with exactly 'CALENDAR_CARDS: [{...}]'");
            contextBuilder.AppendLine("   - CRITICAL: Do NOT add spaces, line breaks, or any other formatting changes");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("2. FOR ANALYSIS/SUMMARY REQUESTS:");
            contextBuilder.AppendLine("   - User asks to 'summarize', 'analyze', 'count', 'describe', or wants 'statistics'");
            contextBuilder.AppendLine("   - For analysis, call functions with analysis-specific parameters when available");
            contextBuilder.AppendLine("   - Parse the structured data and respond in natural conversational language");
            contextBuilder.AppendLine("   - Do NOT include any raw data, JSON, card formats, or technical IDs in your response");
            contextBuilder.AppendLine("   - Do NOT include Microsoft Graph IDs, hash codes, or technical identifiers");
            contextBuilder.AppendLine("   - Focus on meaningful content: titles, subjects, dates, priorities, status, etc.");
            contextBuilder.AppendLine("   - Example: 'You have 3 tasks: \"Buy groceries\" (due tomorrow), \"Meeting prep\" (high priority), and \"Review document\" (completed)'");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("3. FOR NON-STRUCTURED DATA:");
            contextBuilder.AppendLine("   - When functions return plain text (SharePoint, OneDrive info, etc.)");
            contextBuilder.AppendLine("   - Respond in natural language with well-formatted information");
            contextBuilder.AppendLine("   - Do NOT create fake structured data for non-card content");
            contextBuilder.AppendLine("   - Remove any technical IDs or system-generated identifiers from your response");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("IMPORTANT RULES:");
            contextBuilder.AppendLine("- Never include technical IDs, hash codes, or system identifiers in text responses");
            contextBuilder.AppendLine("- For analysis, focus on user-meaningful content only");
            contextBuilder.AppendLine("- Never mix response formats - choose one approach based on user intent");
            contextBuilder.AppendLine("- When in doubt about user intent, prefer the listing/card format");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Available functions:");
            contextBuilder.AppendLine("- Email: GetRecentEmails, SearchEmails, GetEmailsFromSender (return EMAIL_CARDS for display, EMAIL_ANALYSIS for analysis)");
            contextBuilder.AppendLine("- Tasks: GetRecentNotes, SearchNotes, CreateNote, UpdateNoteStatus (return TASK_CARDS for display, TASK_ANALYSIS for analysis)");
            contextBuilder.AppendLine("- Calendar: GetUpcomingEvents, GetTodaysEvents, AddCalendarEvent (return CALENDAR_CARDS for display, CALENDAR_ANALYSIS for analysis)");
            contextBuilder.AppendLine("- OneDrive: GetOneDriveInfo, GetOneDriveFiles (return plain text)");
            contextBuilder.AppendLine("- SharePoint: search_coffeenet_sites, find_coffeenet_sites_by_keyword (return plain text)");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("FUNCTION CALLING STRATEGY:");
            contextBuilder.AppendLine("- For listing/display requests: Call functions with analysisMode=false (default)");
            contextBuilder.AppendLine("- For analysis/summary requests: Call functions with analysisMode=true to get full content");
            contextBuilder.AppendLine("- Analysis functions return TASK_ANALYSIS, EMAIL_ANALYSIS, etc. with full content");
            contextBuilder.AppendLine("- Parse analysis data and respond in natural language without technical details");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Current user: {userName} (ID: {userId})");
            contextBuilder.AppendLine($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            contextBuilder.AppendLine();
            
            foreach (var historyMessage in conversationHistory.TakeLast(5))
            {
                if (!historyMessage.IsAiResponse)
                {
                    contextBuilder.AppendLine($"User: {historyMessage.Content}");
                }
                else
                {
                    // Only include the first 200 characters of assistant responses to avoid token bloat
                    var content = historyMessage.Content.Length > 200 
                        ? historyMessage.Content[..200] + "..." 
                        : historyMessage.Content;
                    contextBuilder.AppendLine($"Assistant: {content}");
                }
            }

            contextBuilder.AppendLine($"\nCurrent user request: {message.Content}");

            // Use Semantic Kernel to process the message with automatic function calling
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 2000,
                Temperature = 0.1
            };

            var response = await userKernel.InvokePromptAsync(contextBuilder.ToString(), new KernelArguments(executionSettings));
            var responseContent = response.ToString();

            // Use the ResponseProcessingService to create a structured response
            var structuredResponse = _responseProcessingService.ProcessResponse(
                responseContent, 
                message.SessionId, 
                userId, 
                userName);

            // Store response in conversation history (convert back to ChatMessage for storage)
            var responseMessage = new ChatMessage
            {
                Id = structuredResponse.Id,
                SessionId = structuredResponse.SessionId,
                Content = structuredResponse.Content,
                UserId = structuredResponse.UserId,
                UserName = structuredResponse.UserName,
                IsAiResponse = structuredResponse.IsAiResponse,
                Timestamp = structuredResponse.Timestamp,
                // Preserve the structured data
                Cards = structuredResponse.Cards,
                Metadata = structuredResponse.Metadata
            };

            await _conversationMemory.AddMessageAsync(responseMessage);

            return Ok(structuredResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, "Error processing message");
        }
    }

    private async Task<Kernel> CreateUserContextKernelAsync(string graphUserAccessToken, string userId, string userName)
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("SemanticKernel").Get<SemanticKernelConfig>()
            ?? throw new InvalidOperationException("SemanticKernel configuration is missing");

        // Get the original access token sent by the client to this API
        // This token will be used by McpServer for its OBO flow.
        var apiAccessToken = await HttpContext.GetTokenAsync("access_token");

        var kernelBuilder = Kernel.CreateBuilder();

        // Add the AI service (same as global kernel)
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
        if (!string.IsNullOrEmpty(graphUserAccessToken))
        {
            // Create plugin instances with dependency injection
            var graphService = HttpContext.RequestServices.GetRequiredService<IGraphService>();
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<CalendarPlugin>>();

            var calendarPlugin = new CalendarPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<CalendarPlugin>>());
            var todoPlugin = new ToDoPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<ToDoPlugin>>());

            // Add Microsoft Graph plugins
            kernelBuilder.Plugins.AddFromObject(new OneDrivePlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>()));
            kernelBuilder.Plugins.AddFromObject(calendarPlugin);
            kernelBuilder.Plugins.AddFromObject(new MailPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(todoPlugin);

            // Add SharePoint MCP plugin (temporary approach until official SK MCP connectors are available for C#)
            var mcpClientService = HttpContext.RequestServices.GetRequiredService<IMcpClientService>();
            var sharePointPlugin = new SharePointMcpPlugin(mcpClientService, HttpContext.RequestServices.GetRequiredService<ILogger<SharePointMcpPlugin>>());
            kernelBuilder.Plugins.AddFromObject(sharePointPlugin);

            _logger.LogInformation("Added OneDrive, Calendar, Mail, ToDo, and SharePoint MCP plugins for user {UserId}.", userId);
        }

        var kernel = kernelBuilder.Build();

        // Store user context in kernel for plugins to use
        if (!string.IsNullOrEmpty(graphUserAccessToken))
        {
            kernel.Data["UserAccessToken"] = graphUserAccessToken;
            kernel.Data["ApiUserAccessToken"] = apiAccessToken;
            kernel.Data["UserId"] = userId ?? string.Empty;
            kernel.Data["UserName"] = userName ?? string.Empty;

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
}