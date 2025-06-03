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
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using SemanticKernelPoc.Api.Services.Shared;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Dynamic;
using ChatMessage = SemanticKernelPoc.Api.Models.ChatMessage;
using ChatResponse = SemanticKernelPoc.Api.Models.ChatResponse;

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ILogger<ChatController> logger,
    IConversationMemoryService conversationMemory,
    IConfiguration configuration,
    ICardBuilderService cardBuilder,
    IAnalysisModeService analysisMode,
    ITextProcessingService textProcessor) : ControllerBase
{
    private readonly ILogger<ChatController> _logger = logger;
    private readonly IConversationMemoryService _conversationMemory = conversationMemory;
    private readonly IConfiguration _configuration = configuration;
    private readonly ICardBuilderService _cardBuilder = cardBuilder;
    private readonly IAnalysisModeService _analysisMode = analysisMode;
    private readonly ITextProcessingService _textProcessor = textProcessor;

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostMessage([FromBody] ChatMessage chatMessage)
    {
        try
        {
            _logger.LogInformation("Received message from user: {UserId}", chatMessage.UserId);

            // Generate unique session ID for the user
            var sessionId = $"session_{chatMessage.UserId}_{DateTime.UtcNow:yyyyMMdd}";
            
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

            _logger.LogInformation("AI response generated with {Length} characters", aiResponse.Length);
            
            // Check for structured data in kernel and build appropriate response
            ChatResponse processedResponse;
            if (kernel.Data.TryGetValue("HasStructuredData", out var hasStructured) && hasStructured?.ToString() == "true")
            {
                _logger.LogInformation("Processing response with structured data from kernel");
                
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
                    Content = cleanResponse,
                    UserId = "ai-assistant",
                    UserName = "AI Assistant",
                    IsAiResponse = true,
                    Timestamp = DateTime.UtcNow,
                    Cards = new CardData
                    {
                        Type = structuredType,
                        Data = structuredData,
                        Count = Convert.ToInt32(structuredCount),
                        UserName = chatMessage.UserName
                    }
                };
            }
            else
            {
                // Regular text response without structured data
                processedResponse = new ChatResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    Content = aiResponse,
                    UserId = "ai-assistant",
                    UserName = "AI Assistant",
                    IsAiResponse = true,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Save AI response to conversation memory
            var aiChatMessage = new ChatMessage
            {
                Id = processedResponse.Id,
                SessionId = sessionId,
                Content = processedResponse.Content,
                UserId = "ai-assistant",
                UserName = "AI Assistant",
                IsAiResponse = true,
                Timestamp = DateTime.UtcNow
            };

            await _conversationMemory.AddMessageAsync(aiChatMessage);

            return Ok(processedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { message = "An error occurred while processing your message." });
        }
    }

    private async Task<Kernel> CreateUserKernelAsync(string userId)
    {
        var builder = Kernel.CreateBuilder();

        // Add AI service
        var openAiKey = _configuration["AzureOpenAI:ApiKey"];
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiModel = _configuration["AzureOpenAI:DeploymentName"];

        builder.AddAzureOpenAIChatCompletion(openAiModel, openAiEndpoint, openAiKey);

        var kernel = builder.Build();

        // Add user context to kernel
        kernel.Data["UserId"] = userId;

        try
        {
            // Get user access token for Microsoft Graph
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                kernel.Data["AccessToken"] = accessToken;

                // Add Microsoft Graph plugins
                var graphClientFactory = HttpContext.RequestServices.GetRequiredService<IGraphClientFactory>();
                var graphService = HttpContext.RequestServices.GetRequiredService<IGraphService>();

                var taskPlugin = new ToDoPlugin(graphService, graphClientFactory, 
                    HttpContext.RequestServices.GetRequiredService<ILogger<ToDoPlugin>>(),
                    _cardBuilder, _analysisMode, _textProcessor);
                
                var calendarPlugin = new CalendarPlugin(graphService, graphClientFactory,
                    HttpContext.RequestServices.GetRequiredService<ILogger<CalendarPlugin>>(),
                    _cardBuilder, _analysisMode, _textProcessor);
                
                var mailPlugin = new MailPlugin(graphService, graphClientFactory,
                    HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>(),
                    _cardBuilder, _analysisMode, _textProcessor);
                
                var oneDrivePlugin = new OneDrivePlugin(graphService, graphClientFactory,
                    HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>());

                kernel.Plugins.AddFromObject(taskPlugin, "Tasks");
                kernel.Plugins.AddFromObject(calendarPlugin, "Calendar");
                kernel.Plugins.AddFromObject(mailPlugin, "Mail");
                kernel.Plugins.AddFromObject(oneDrivePlugin, "OneDrive");

                // Add MCP tools for SharePoint
                await AddMcpToolsToKernelAsync(kernel, accessToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up user plugins for user: {UserId}", userId);
        }

        return kernel;
    }

    private async Task AddMcpToolsToKernelAsync(Kernel kernel, string userAccessToken)
    {
        try
        {
            _logger.LogInformation("Initializing MCP integration for SharePoint tools");
            
            var mcpServerUrl = _configuration["McpServer:Url"] ?? "https://localhost:31339";
            
            try
            {
                var mcpServerEndpoint = new Uri($"{mcpServerUrl}/sse");
                
                var sseClientTransportOptions = new SseClientTransportOptions
                {
                    Endpoint = mcpServerEndpoint,
                    Name = "SharePointMCP"
                };
                
                var sseClientTransport = new SseClientTransport(sseClientTransportOptions);
                var mcpClient = await McpClientFactory.CreateAsync(sseClientTransport);
                
                _logger.LogInformation("MCP client connection established");
                
                var mcpTools = await mcpClient.ListToolsAsync();
                
                if (mcpTools?.Any() == true)
                {
                    _logger.LogInformation("Found {ToolCount} MCP tools", mcpTools.Count());
                    
                    var kernelFunctions = new List<KernelFunction>();
                    var toolIndex = 0;
                    
                    foreach (var tool in mcpTools)
                    {
                        var kernelFunction = CreateKernelFunctionFromMcpTool(tool, mcpClient, userAccessToken, toolIndex, kernel);
                        kernelFunctions.Add(kernelFunction);
                        toolIndex++;
                    }
                    
                    kernel.Plugins.AddFromFunctions("SharePointMCP", kernelFunctions);
                    _logger.LogInformation("Added {FunctionCount} SharePoint MCP functions to kernel", kernelFunctions.Count);
                }
                else
                {
                    _logger.LogWarning("No MCP tools found - SharePoint functionality will be unavailable");
                }
            }
            catch (Exception transportEx)
            {
                _logger.LogError(transportEx, "Failed to create MCP client connection");
                _logger.LogInformation("SharePoint functionality will be unavailable. Ensure MCP server is running on {Url}", mcpServerUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MCP integration");
        }
    }

    private KernelFunction CreateKernelFunctionFromMcpTool(object tool, IMcpClient mcpClient, string userAccessToken, int toolIndex, Kernel kernel)
    {
        var toolName = tool.GetType().GetProperty("Name")?.GetValue(tool)?.ToString() ?? $"McpTool{toolIndex}";
        var toolDescription = tool.GetType().GetProperty("Description")?.GetValue(tool)?.ToString() ?? "MCP tool from SharePoint server";
        
        _logger.LogInformation("Creating kernel function for MCP tool: {ToolName}", toolName);
        
        return KernelFunctionFactory.CreateFromMethod(
            async ([Description("Search query")] string query = "", 
             [Description("Number of results")] int count = 5, 
             [Description("List name")] string listName = "") =>
            {
                return await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Calling MCP tool: {ToolName} with query: '{Query}', count: {Count}", toolName, query, count);
                        
                        var parameters = new Dictionary<string, object>();
                        
                        if (!string.IsNullOrEmpty(query))
                            parameters["query"] = query;
                        
                        if (count > 0)
                            parameters["count"] = count;
                        
                        if (!string.IsNullOrEmpty(listName))
                            parameters["listName"] = listName;
                        
                        if (!string.IsNullOrEmpty(userAccessToken))
                            parameters["accessToken"] = userAccessToken;
                        else
                            _logger.LogError("Access token is missing for SharePoint MCP call");
                        
                        // Alternative parameter names for compatibility
                        parameters["searchQuery"] = query;
                        parameters["limit"] = count;
                        parameters["maxResults"] = count;
                        
                        // Determine response mode based on query context
                        var isAnalysisRequest = IsAnalysisRequest(query);
                        var responseMode = isAnalysisRequest ? "analysis" : "card";
                        parameters["responseMode"] = responseMode;
                        
                        _logger.LogInformation("MCP call parameters prepared, response mode: {ResponseMode}", responseMode);
                        
                        var result = await mcpClient.CallToolAsync(toolName, parameters);
                        
                        if (result?.Content?.Count > 0)
                        {
                            var resultString = result.Content.First()?.Text ?? "";
                            _logger.LogInformation("MCP call completed successfully, response length: {Length} characters", resultString.Length);
                            
                            // Handle SharePoint tools with structured responses
                            if (IsSharePointTool(toolName))
                            {
                                return await ProcessSharePointResponse(kernel, resultString, toolName, query, isAnalysisRequest);
                            }
                            
                            return resultString;
                        }
                        
                        _logger.LogWarning("MCP tool returned no content");
                        return "No results found.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calling MCP tool: {ToolName}", toolName);
                        return $"Error executing {toolName}: {ex.Message}";
                    }
                });
            },
            toolName,
            toolDescription
        );
    }

    [HttpGet("sessions/{sessionId}/history")]
    public async Task<ActionResult<IEnumerable<ChatMessage>>> GetConversationHistory(string sessionId, [FromQuery] int maxMessages = 20)
    {
        try
        {
            var history = await _conversationMemory.GetConversationHistoryAsync(sessionId, maxMessages);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for session: {SessionId}", sessionId);
            return StatusCode(500, new { message = "Error retrieving conversation history" });
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult> ClearConversation(string sessionId)
    {
        try
        {
            await _conversationMemory.ClearConversationAsync(sessionId);
            return Ok(new { message = "Conversation cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation for session: {SessionId}", sessionId);
            return StatusCode(500, new { message = "Error clearing conversation" });
        }
    }

    private string CreateEnhancedSystemPrompt(string userName)
    {
        return $@"You are an intelligent assistant helping {userName} with their tasks, emails, calendar, OneDrive files, and SharePoint sites.

CRITICAL RULES:
- NEVER make up data - ONLY use actual results from function calls
- For SharePoint queries, ALWAYS call the appropriate SharePoint functions
- When users ask for SharePoint sites, tasks, emails, or calendar events, immediately call the relevant functions
- Use structured data responses when available
- Be helpful and natural in your responses

Available functions:
- Tasks: GetRecentNotes, SearchNotes, CreateNote, UpdateNoteStatus, GetTaskLists
- Calendar: GetRecentEvents, SearchEvents, CreateEvent  
- Mail: GetRecentEmails, SearchEmails, SendEmail
- OneDrive: GetRecentFiles, SearchFiles, UploadFile
- SharePoint: SearchSharePointSites, SearchRecentSharePointSites

When users ask for information, call the appropriate functions to get real data, then provide helpful responses based on the actual results.";
    }

    private string BuildConversationContext(List<ChatMessage> history, string systemPrompt, string currentMessage)
    {
        var context = new StringBuilder();
        context.AppendLine(systemPrompt);
        context.AppendLine();

        // Add recent conversation history (last 10 messages)
        var recentHistory = history.TakeLast(10);
        foreach (var message in recentHistory)
        {
            var role = message.IsAiResponse ? "Assistant" : "User";
            context.AppendLine($"{role}: {message.Content}");
        }

        context.AppendLine($"User: {currentMessage}");
        context.AppendLine("Assistant:");

        return context.ToString();
    }

    private string GenerateSessionId(string userId)
    {
        return $"session_{userId}_{DateTime.UtcNow:yyyyMMdd}";
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserSessions()
    {
        try
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID not found");
            }

            var sessions = await _conversationMemory.GetUserSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user sessions");
            return StatusCode(500, new { message = "Error retrieving sessions" });
        }
    }

    private bool IsSharePointTool(string toolName)
    {
        return toolName.Contains("SharePoint", StringComparison.OrdinalIgnoreCase) ||
               toolName.Contains("Sites", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAnalysisRequest(string query)
    {
        var analysisKeywords = new[] { 
            "summarize", "summary", "analyze", "analysis", "what about", "content overview",
            "which", "what should I", "recommend", "priority", "prioritize", "tackle first",
            "focus on", "start with", "most important", "urgent", "advice", "suggest",
            "help me decide", "what to do"
        };
        
        return analysisKeywords.Any(keyword => query.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ProcessSharePointResponse(Kernel kernel, string resultString, string toolName, string query, bool isAnalysisMode)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(resultString);
            if (jsonDoc.RootElement.TryGetProperty("success", out var successElement) && 
                successElement.GetBoolean() &&
                jsonDoc.RootElement.TryGetProperty("sites", out var sitesElement))
            {
                var sites = JsonSerializer.Deserialize<object[]>(sitesElement.GetRawText());
                var message = jsonDoc.RootElement.TryGetProperty("message", out var messageElement) 
                    ? messageElement.GetString() : $"Found {sites.Length} SharePoint sites.";
                
                // Set kernel data for structured response
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "sharepoint";
                kernel.Data["StructuredDataCount"] = sites.Length;
                kernel.Data["SharePointCards"] = sites;
                kernel.Data["SharePointFunctionResponse"] = message;
                
                _logger.LogInformation("Processed SharePoint response with {Count} sites", sites.Length);
                
                return message ?? $"Found {sites.Length} SharePoint sites.";
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse SharePoint response as JSON");
        }
        
        return resultString;
    }
}