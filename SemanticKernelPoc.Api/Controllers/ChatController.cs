using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Plugins.Calendar;
using SemanticKernelPoc.Api.Plugins.Mail;
using SemanticKernelPoc.Api.Plugins.OneDrive;
using SemanticKernelPoc.Api.Plugins.ToDo;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.Graph;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ILogger<ChatController> logger,
    IConversationMemoryService conversationMemory) : ControllerBase
{
    private readonly ILogger<ChatController> _logger = logger;
    private readonly IConversationMemoryService _conversationMemory = conversationMemory;

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
            
            // Check for structured data in kernel instead of parsing the AI response
            ChatResponse processedResponse;
            if (kernel.Data.TryGetValue("HasStructuredData", out var hasStructured) && hasStructured?.ToString() == "true")
            {
                _logger.LogInformation("✅ Processing response with structured data from kernel");
                
                var structuredType = kernel.Data.TryGetValue("StructuredDataType", out var type) ? type?.ToString() : "tasks";
                var structuredCount = kernel.Data.TryGetValue("StructuredDataCount", out var count) ? count : 0;
                
                // Get the appropriate structured data based on type
                object structuredData = null;
                string functionResponse = null;
                
                switch (structuredType)
                {
                    case "tasks":
                        structuredData = kernel.Data.TryGetValue("TasksCards", out var taskData) ? taskData : null;
                        functionResponse = kernel.Data.TryGetValue("TasksFunctionResponse", out var taskResponse) ? taskResponse?.ToString() : null;
                        break;
                    case "emails":
                        structuredData = kernel.Data.TryGetValue("EmailsCards", out var emailData) ? emailData : null;
                        functionResponse = kernel.Data.TryGetValue("EmailsFunctionResponse", out var emailResponse) ? emailResponse?.ToString() : null;
                        break;
                    case "calendar":
                        structuredData = kernel.Data.TryGetValue("CalendarCards", out var calendarData) ? calendarData : null;
                        functionResponse = kernel.Data.TryGetValue("CalendarFunctionResponse", out var calendarResponse) ? calendarResponse?.ToString() : null;
                        break;
                    case "sharepoint":
                        structuredData = kernel.Data.TryGetValue("SharePointCards", out var sharePointData) ? sharePointData : null;
                        functionResponse = kernel.Data.TryGetValue("SharePointFunctionResponse", out var sharePointResponse) ? sharePointResponse?.ToString() : null;
                        break;
                    default:
                        structuredData = kernel.Data.TryGetValue("TasksCards", out var defaultData) ? defaultData : null;
                        functionResponse = kernel.Data.TryGetValue("TasksFunctionResponse", out var defaultResponse) ? defaultResponse?.ToString() : null;
                        break;
                }
                
                // Use function response if available, otherwise use a clean minimal response
                var cleanResponse = functionResponse ?? $"I found {structuredCount} {structuredType}. The details are displayed in the cards below.";
                
                processedResponse = new ChatResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    Content = cleanResponse, // Use function response instead of AI generated content
                    UserId = "ai-assistant",
                    UserName = "AI Assistant",
                    IsAiResponse = true,
                    Timestamp = DateTime.UtcNow,
                    Cards = new CardData
                    {
                        Type = structuredType,
                        Data = structuredData,
                        Count = Convert.ToInt32(structuredCount),
                        UserName = chatMessage.UserName,
                        TimeRange = "Recent"
                    },
                    Metadata = new ResponseMetadata
                    {
                        HasCards = true
                    }
                };
                
                _logger.LogInformation("📊 Created structured response - Type: {Type}, Count: {Count}", structuredType, structuredCount);
                _logger.LogInformation("📝 Using function response: {FunctionResponse}", cleanResponse);
            }
            else
            {
                _logger.LogInformation("📝 Processing as regular text response (no structured data)");
                
                // Create regular text response
                processedResponse = new ChatResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    Content = aiResponse,
                    UserId = "ai-assistant",
                    UserName = "AI Assistant",
                    IsAiResponse = true,
                    Timestamp = DateTime.UtcNow,
                    Metadata = new ResponseMetadata
                    {
                        HasCards = false
                    }
                };
            }
            
            _logger.LogInformation("=== AI RESPONSE ANALYSIS END ===");
                 
            _logger.LogInformation("=== PROCESSED RESPONSE ANALYSIS START ===");
            _logger.LogInformation("Processed Content Length: {Length} characters", processedResponse.Content?.Length ?? 0);
            _logger.LogInformation("Processed Content: {Content}", processedResponse.Content);
            _logger.LogInformation("Has Cards: {HasCards}", processedResponse.Cards != null);
            if (processedResponse.Cards != null)
            {
                _logger.LogInformation("Card Type: {Type}, Count: {Count}", processedResponse.Cards.Type, processedResponse.Cards.Count);
                
                // DEBUG: Log the actual cards data being sent
                try
                {
                    var cardsJson = System.Text.Json.JsonSerializer.Serialize(processedResponse.Cards.Data);
                    _logger.LogInformation("🔍 DEBUG - Cards Data JSON being sent: {CardsJson}", cardsJson);
                    _logger.LogInformation("🔍 DEBUG - Cards Data Type: {DataType}", processedResponse.Cards.Data?.GetType().Name ?? "null");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to serialize cards data for debugging");
                }
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

        // Configure AI service using the correct config structure
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

        var kernel = kernelBuilder.Build();

        // Register plugins
        var graphService = HttpContext.RequestServices.GetRequiredService<IGraphService>();
        var serviceProvider = HttpContext.RequestServices;
        
        // Get user access token and add to kernel data for plugins
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var userName = User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value ?? "User";
        var userOid = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ?? "Unknown";
        
        // Add user context to kernel data for plugins to access
        kernel.Data["UserAccessToken"] = accessToken;
        kernel.Data["UserName"] = userName;
        kernel.Data["UserId"] = userOid;
        
        _logger.LogInformation("🔑 TOKEN DEBUG: Adding user context to kernel data");
        _logger.LogInformation("   👤 User Name: {UserName}", userName);
        _logger.LogInformation("   🆔 User OID: {UserOid}", userOid);
        _logger.LogInformation("   🎫 Access Token Length: {TokenLength} chars", accessToken?.Length ?? 0);
        _logger.LogInformation("   📋 Kernel Data Keys: {Keys}", string.Join(", ", kernel.Data.Keys));
        
        // Add standard plugins directly
        kernel.Plugins.AddFromType<OneDrivePlugin>("OneDrivePlugin", serviceProvider);
        kernel.Plugins.AddFromType<CalendarPlugin>("CalendarPlugin", serviceProvider);
        kernel.Plugins.AddFromType<MailPlugin>("MailPlugin", serviceProvider);
        kernel.Plugins.AddFromType<ToDoPlugin>("ToDoPlugin", serviceProvider);

        // Add SharePoint MCP Plugin with proper token context
        if (!string.IsNullOrEmpty(accessToken))
        {
            // The user token is already added to kernel.Data above, which the SharePoint plugin will access
            // Register SharePoint MCP Plugin
            kernel.Plugins.AddFromType<SemanticKernelPoc.Api.Plugins.SharePoint.SharePointMcpPlugin>("SharePointMcpPlugin", serviceProvider);
            
            _logger.LogInformation("Added OneDrive, Calendar, Mail, ToDo, and SharePoint MCP plugins for user {UserId}.", userOid);
        }
        else
        {
            _logger.LogWarning("⚠️ No access token available for SharePoint MCP plugin");
            
            // Add other plugins without SharePoint
            _logger.LogInformation("Added OneDrive, Calendar, Mail, and ToDo plugins for user {UserId} (SharePoint unavailable due to missing token).", userOid);
        }

        // Log available functions for debugging
        var availableFunctions = kernel.Plugins.SelectMany(p => p.Select(f => $"{p.Name}.{f.Name}")).ToList();
        _logger.LogInformation("Available functions for user {UserId}: {Functions}", userOid, string.Join(", ", availableFunctions));

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

CRITICAL: YOU MUST CALL FUNCTIONS - DO NOT GIVE GENERIC RESPONSES!

MANDATORY FUNCTION CALLING RULES:

1. SHAREPOINT SITES - When user asks for SharePoint sites, you MUST call one of these functions:
   - SearchSharePointSites - for general ""SharePoint sites"" requests
   - SearchRecentSharePointSites - for ""recent"", ""last N"", ""latest"" requests  
   - FindSharePointSitesByKeyword - for specific search terms
   - SearchSharePointSitesAdvanced - for complex queries
   
   Example: User says ""show me my last 3 sharepoint sites"" → CALL SearchRecentSharePointSites with count=3

2. TASK/TODO QUERIES - ALWAYS use ToDoPlugin functions for:
   - ""my tasks"", ""show tasks"", ""get tasks"", ""task list"", ""to-do"", ""todos""
   - ""what tasks do I have"", ""recent tasks"", ""task summary""
   - Task-related searches, creation, and management
   
   IMPORTANT: Use analysisMode=true for:
   - ""which task should I tackle first"", ""what should I focus on"", ""prioritize my tasks""
   - ""summarize my tasks"", ""what are my tasks about"", ""task summary""
   - ""recommend"", ""suggest"", ""advice"", ""help me decide"", ""most important""
   - Any request asking for analysis, advice, recommendations, or prioritization
   
   Use analysisMode=false ONLY for simple listing/display requests like ""show my tasks""

3. EMAIL QUERIES - ALWAYS use MailPlugin functions for:
   - ""emails"", ""messages"", ""inbox"", ""mail""
   - ""recent emails"", ""check email"", ""email from [sender]""

4. CALENDAR QUERIES - ALWAYS use CalendarPlugin functions for:
   - ""meetings"", ""appointments"", ""calendar"", ""events""
   - ""today's meetings"", ""upcoming events"", ""schedule""

5. ONEDRIVE QUERIES - ALWAYS use OneDrivePlugin functions for:
   - ""files"", ""documents"", ""OneDrive""
   - ""recent files"", ""my documents""

NEVER say ""I cannot access"" or ""I don't have the ability"" - these functions ARE available.
ALWAYS attempt to call the appropriate function first.
If a function call fails, THEN explain the error from the function result.

When users ask for data, you MUST call the relevant functions to retrieve actual data.
Do not make assumptions or provide generic responses without calling functions.

Your role is to be a helpful Microsoft 365 assistant that actually retrieves and works with user data through the available functions.

Always be helpful, accurate, and call the appropriate functions to fulfill user requests.";
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

    private string GenerateSessionId(string userId)
    {
        return $"session_{userId}_{DateTime.UtcNow:yyyyMMdd}";
    }
}