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
using SemanticKernelPoc.Api.Services;
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

            // Add MCP function call logging to track when SharePoint functions are called
            kernel.FunctionInvoking += (sender, e) =>
            {
                var functionFullName = $"{e.Function.PluginName}.{e.Function.Name}";
                
                if (e.Function.PluginName.StartsWith("SharePointMCP"))
                {
                    _logger.LogWarning("🚨 CRITICAL: SharePoint MCP function called - {FunctionName}", functionFullName);
                    _logger.LogWarning("   📨 Original user message: {UserMessage}", chatMessage.Content);
                    _logger.LogWarning("   🤔 This should NOT happen for task-related requests!");
                    
                    // Log parameters for debugging
                    foreach (var param in e.Arguments)
                    {
                        _logger.LogWarning("   📋 Parameter {Key}: {Value}", param.Key, param.Value);
                    }
                }
                else if (e.Function.PluginName == "ToDoPlugin")
                {
                    _logger.LogInformation("✅ CORRECT: ToDo function called for user request - {FunctionName}", functionFullName);
                    _logger.LogInformation("   📨 User message: {UserMessage}", chatMessage.Content);
                }
                else
                {
                    _logger.LogInformation("🔧 Function Call: {FunctionName}", functionFullName);
                    _logger.LogInformation("   📨 User message: {UserMessage}", chatMessage.Content);
                }
            };

            kernel.FunctionInvoked += (sender, e) =>
            {
                var resultPreview = e.Result?.GetValue<object>()?.ToString();
                var truncatedResult = resultPreview?.Length > 200 ? resultPreview.Substring(0, 200) + "..." : resultPreview;
                
                if (e.Function.PluginName.StartsWith("SharePointMCP"))
                {
                    _logger.LogInformation("📡 MCP Function Result: {PluginName}.{FunctionName} completed", 
                        e.Function.PluginName, e.Function.Name);
                    
                    if (!string.IsNullOrEmpty(resultPreview))
                    {
                        _logger.LogInformation("   📤 Result preview: {ResultPreview}", truncatedResult);
                    }
                    
                    // Check for authentication errors in the result
                    if (resultPreview?.Contains("MsalUiRequiredException") == true || 
                        resultPreview?.Contains("additional permissions") == true ||
                        resultPreview?.Contains("requires additional consent") == true)
                    {
                        _logger.LogWarning("⚠️ SharePoint MCP function returned authentication error: {FunctionName}", e.Function.Name);
                        _logger.LogWarning("   🔑 Error details: {ErrorDetails}", truncatedResult);
                    }
                }
                else
                {
                    _logger.LogInformation("✅ Function Result: {PluginName}.{FunctionName} completed", 
                        e.Function.PluginName, e.Function.Name);
                }
                
                // Check for structured data stored in kernel data after function execution
                if (kernel.Data.ContainsKey("TaskCards"))
                {
                    _logger.LogInformation("📊 Structured task data found in kernel data");
                    var taskCards = kernel.Data["TaskCards"];
                    var cardType = kernel.Data.TryGetValue("TaskCardType", out var type) ? type?.ToString() : "tasks";
                    var cardCount = kernel.Data.TryGetValue("TaskCardCount", out var count) ? count : 0;
                    
                    _logger.LogInformation("   🎯 Card Type: {CardType}, Count: {Count}", cardType, cardCount);
                    
                    // Store structured data for response processing
                    kernel.Data["HasStructuredData"] = true;
                    kernel.Data["StructuredDataType"] = cardType;
                    kernel.Data["StructuredDataCount"] = cardCount;
                }
            };

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
                switch (structuredType)
                {
                    case "tasks":
                        structuredData = kernel.Data.TryGetValue("TaskCards", out var taskData) ? taskData : null;
                        break;
                    case "emails":
                        structuredData = kernel.Data.TryGetValue("EmailCards", out var emailData) ? emailData : null;
                        break;
                    case "calendar":
                        structuredData = kernel.Data.TryGetValue("CalendarCards", out var calendarData) ? calendarData : null;
                        break;
                    default:
                        structuredData = kernel.Data.TryGetValue("TaskCards", out var defaultData) ? defaultData : null;
                        break;
                }
                
                processedResponse = new ChatResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    Content = aiResponse, // Clean AI response without prefixes
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
            }
            else
            {
                _logger.LogInformation("📝 Processing as regular text response (no structured data)");
                
                // Process the response using the old method for backward compatibility (in case some functions still use prefixes)
                processedResponse = _responseProcessingService.ProcessResponse(
                    aiResponse, 
                    sessionId, 
                    "ai-assistant", 
                    "AI Assistant");
            }
            
            _logger.LogInformation("=== AI RESPONSE ANALYSIS END ===");
                 
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

        // Register MCP tools using HTTPS communication
        if (!string.IsNullOrEmpty(accessToken))
        {
            await RegisterMcpToolsAsync(kernel, accessToken);
        }
        else
        {
            _logger.LogWarning("⚠️ No access token available for MCP tools registration");
        }

        return kernel;
    }

    private async Task RegisterMcpToolsAsync(Kernel kernel, string userAccessToken)
    {
        try
        {
            _logger.LogInformation("🚀 Starting MCP Client Integration following proper pattern");
            _logger.LogInformation("📋 User access token length: {TokenLength} characters", userAccessToken?.Length ?? 0);
            
            // Create an MCP client using the proper pattern (but with HTTPS instead of STDIO)
            await using IMcpClient mcpClient = await CreateMcpClientAsync(userAccessToken);

            // Retrieve and display the list provided by the MCP server
            IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
            DisplayTools(tools);

            // Create a kernel and register the MCP tools
            kernel.Plugins.AddFromFunctions("SharePointMCP", tools.Select(tool => tool.AsKernelFunction()));
            
            _logger.LogInformation("✅ Successfully registered {ToolCount} MCP tools", tools.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to register MCP tools: {ErrorMessage}", ex.Message);
            // Don't rethrow - continue without MCP tools
        }
    }

    private async Task<IMcpClient> CreateMcpClientAsync(string userAccessToken)
    {
        try
        {
            _logger.LogInformation("🔧 Creating MCP client for HTTPS communication...");
            
            // Create HTTPS-based MCP client using SSE transport (following sample pattern but with SSE for HTTPS)
            var mcpClient = await McpClientFactory.CreateAsync(
                clientTransport: new SseClientTransport(new SseClientTransportOptions
                {
                    Endpoint = new Uri("https://localhost:31339/sse")
                })
            );
            
            _logger.LogInformation("✅ MCP client created successfully");
            return mcpClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create MCP client: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private void DisplayTools(IList<McpClientTool> tools)
    {
        _logger.LogInformation("📋 Available MCP tools:");
        foreach (var tool in tools)
        {
            _logger.LogInformation("   • Name: {Name}, Description: {Description}", tool.Name, tool.Description);
        }
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

CRITICAL FUNCTION SELECTION RULES:

1. TASK/TODO QUERIES - Use ONLY ToDoPlugin functions:
   - ""my tasks"", ""show tasks"", ""get tasks"", ""task list"", ""to-do"", ""todos""
   - ""what tasks do I have"", ""recent tasks"", ""task summary""
   - NEVER call SharePointMCP functions for task requests
   - Use: ToDoPlugin.GetRecentNotes, ToDoPlugin.SearchNotes, ToDoPlugin.CreateNote

2. SHAREPOINT QUERIES - Use ONLY SharePointMCP functions:
   - ""SharePoint sites"", ""find sites"", ""search sites""
   - ""workspace sites"", ""team sites"", ""collaboration sites""
   - Use: SharePointMCP.search_sharepoint_sites, SharePointMCP.find_sharepoint_sites_by_keyword

3. EMAIL QUERIES - Use ONLY MailPlugin functions:
   - ""emails"", ""messages"", ""inbox"", ""mail""
   - Use: MailPlugin.GetRecentEmails, MailPlugin.SearchEmails

4. CALENDAR QUERIES - Use ONLY CalendarPlugin functions:
   - ""calendar"", ""meetings"", ""appointments"", ""events""
   - Use: CalendarPlugin.GetUpcomingEvents, CalendarPlugin.GetTodaysEvents

5. ONEDRIVE/FILES QUERIES - Use ONLY OneDrivePlugin functions:
   - ""files"", ""OneDrive"", ""documents""
   - Use: OneDrivePlugin.GetOneDriveFiles, OneDrivePlugin.GetOneDriveInfo

CRITICAL RESPONSE FORMAT RULES:

1. CARD DISPLAY vs ANALYSIS MODE:
   - For DISPLAY requests (""show"", ""list"", ""get"", ""find"", ""my tasks"", etc.): Use default function parameters to get cards
   - For ANALYSIS requests (""summarize"", ""analyze"", ""what are my tasks about"", ""overview"", etc.): Set analysisMode=true

2. NATURAL LANGUAGE RESPONSES:
   - Functions return clean natural language responses (e.g., ""Found 3 recent tasks for {userName}"")
   - Structured data for UI cards is handled automatically by the system
   - Provide the function response directly to the user - no additional formatting needed
   - Do NOT try to format or modify function responses

EXAMPLES OF CORRECT FUNCTION CALLS:

Task Request: ""show my tasks""
✅ CORRECT: ToDoPlugin.GetRecentNotes()
❌ WRONG: SharePointMCP functions - these are for sites only!

Task Analysis: ""summarize my tasks""
✅ CORRECT: ToDoPlugin.GetRecentNotes(analysisMode=true)
❌ WRONG: SharePointMCP functions - these are for sites only!

SharePoint Request: ""find SharePoint sites""
✅ CORRECT: SharePointMCP.search_sharepoint_sites()
❌ WRONG: ToDoPlugin functions - these are for tasks only!

EXAMPLES OF CORRECT RESPONSES:

Display Request:
User: 'show my tasks'
Function Call: ToDoPlugin.GetRecentNotes(analysisMode=false)
Function Returns: 'Found 3 recent tasks for {userName}.'
Correct Response: 'Found 3 recent tasks for {userName}.' (cards will display automatically)

Analysis Request:
User: 'summarize my tasks'
Function Call: ToDoPlugin.GetRecentNotes(analysisMode=true)
Function Returns: 'Found 3 tasks for {userName}. 1 completed, 1 high priority. Most recent tasks cover: Buy groceries, Prepare presentation, Schedule dentist appointment.'
Correct Response: 'Found 3 tasks for {userName}. 1 completed, 1 high priority. Most recent tasks cover: Buy groceries, Prepare presentation, Schedule dentist appointment.'

FUNCTION USAGE WITH ANALYSIS MODE:
- GetRecentNotes: Use analysisMode=true for ""summarize tasks"", ""task overview"", ""what are my tasks about""
- GetRecentEmails: Use analysisMode=true for ""email summary"", ""what emails did I get""
- SearchNotes: Use analysisMode=true for ""analyze tasks about X"", ""summarize tasks containing Y""
- For display queries (""show"", ""list"", ""get""), use default analysisMode=false

REMEMBER: NEVER mix function types! Tasks = ToDoPlugin ONLY. SharePoint = SharePointMCP ONLY. 

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

    private string GenerateSessionId(string userId)
    {
        return $"session_{userId}_{DateTime.UtcNow:yyyyMMdd}";
    }
}