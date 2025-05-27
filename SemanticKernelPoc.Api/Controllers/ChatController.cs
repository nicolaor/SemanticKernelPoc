using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly Kernel _kernel;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConversationMemoryService _conversationMemory;

    public ChatController(
        ILogger<ChatController> logger,
        Kernel kernel,
        ITokenAcquisition tokenAcquisition,
        IConversationMemoryService conversationMemory)
    {
        _logger = logger;
        _kernel = kernel;
        _tokenAcquisition = tokenAcquisition;
        _conversationMemory = conversationMemory;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatMessage>> SendMessage([FromBody] ChatMessage message)
    {
        var requestStartTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("⏱️ ChatController.SendMessage started at {StartTime}", requestStartTime);
            
            var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            var userName = User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
            
            // Ensure message has required properties
            if (string.IsNullOrEmpty(message.Id))
                message.Id = Guid.NewGuid().ToString();
            if (message.Timestamp == default)
                message.Timestamp = DateTime.UtcNow;
            if (string.IsNullOrEmpty(message.UserId))
                message.UserId = userId ?? "";
            if (string.IsNullOrEmpty(message.UserName))
                message.UserName = userName ?? "";
            if (string.IsNullOrEmpty(message.SessionId))
                message.SessionId = $"session_{userId}_{DateTime.UtcNow:yyyyMMdd}"; // Default session per user per day
            
            _logger.LogInformation("⏱️ Message setup completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Received chat message from user {UserId} ({UserName}) in session {SessionId}: {Content}", 
                userId, userName, message.SessionId, message.Content);

            // Save user message to conversation memory
            var memoryStartTime = stopwatch.ElapsedMilliseconds;
            await _conversationMemory.AddMessageAsync(message);
            _logger.LogInformation("⏱️ Memory save completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - memoryStartTime, stopwatch.ElapsedMilliseconds);

            // Get user's original authentication token (with API audience) for MCP On-Behalf-Of flow
            var tokenStartTime = stopwatch.ElapsedMilliseconds;
            string userAccessToken = null;
            try
            {
                // Get the original authentication token from the Authorization header
                var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer "))
                {
                    userAccessToken = authHeader.Substring("Bearer ".Length);
                    _logger.LogInformation("Successfully extracted user authentication token for user {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("No Bearer token found in Authorization header for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract user authentication token for user {UserId}. Continuing without user context.", userId);
            }
            _logger.LogInformation("⏱️ Token extraction completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - tokenStartTime, stopwatch.ElapsedMilliseconds);

            // Create a user-context kernel with the user's token
            var kernelStartTime = stopwatch.ElapsedMilliseconds;
            var userKernel = CreateUserContextKernel(userAccessToken, userId, userName);
            _logger.LogInformation("⏱️ User kernel creation completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - kernelStartTime, stopwatch.ElapsedMilliseconds);

            // Get the chat completion service
            var chatCompletionService = userKernel.GetRequiredService<IChatCompletionService>();
            
            // Load conversation history and create chat history
            var historyStartTime = stopwatch.ElapsedMilliseconds;
            var conversationHistory = await _conversationMemory.GetConversationHistoryAsync(message.SessionId, maxMessages: 20);
            _logger.LogInformation("⏱️ Conversation history loaded in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - historyStartTime, stopwatch.ElapsedMilliseconds);
            
            var chatHistoryStartTime = stopwatch.ElapsedMilliseconds;
            var chatHistory = new ChatHistory();
            
            // Add system message
            chatHistory.AddSystemMessage(
                $"You are an AI assistant for {userName ?? "the user"}. " +
                "You have access to Microsoft Graph for productivity tasks. " +
                
                "FUNCTION RULES:\n" +
                "• For 'tasks', 'my tasks', 'todos' → call GetRecentNotes()\n" +
                "• For 'today' events → call GetTodaysEvents()\n" +
                "• For 'upcoming' events → call GetUpcomingEvents()\n" +
                "• When functions return 'CALENDAR_CARDS:' or 'TASK_CARDS:' → return ONLY that exact response\n" +
                
                "Be helpful and use functions when needed.");
            
            // Add conversation history (excluding the current message as it's already processed)
            foreach (var historyMessage in conversationHistory.Where(m => m.Id != message.Id).OrderBy(m => m.Timestamp))
            {
                if (historyMessage.IsAiResponse)
                {
                    chatHistory.AddAssistantMessage(historyMessage.Content);
                }
                else
                {
                    chatHistory.AddUserMessage(historyMessage.Content);
                }
            }
            
            // Add the current user message
            chatHistory.AddUserMessage(message.Content);
            _logger.LogInformation("⏱️ Chat history setup completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - chatHistoryStartTime, stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Loaded {HistoryCount} messages from conversation history for session {SessionId}", 
                conversationHistory.Count(), message.SessionId);

            // Fast-path detection for common requests to bypass AI processing
            var fastPathStartTime = stopwatch.ElapsedMilliseconds;
            var fastPathResult = await TryFastPathResponse(message.Content, userKernel);
            if (!string.IsNullOrEmpty(fastPathResult))
            {
                _logger.LogInformation("⏱️ Fast-path response generated in {ElapsedMs}ms (total: {TotalMs}ms)", 
                    stopwatch.ElapsedMilliseconds - fastPathStartTime, stopwatch.ElapsedMilliseconds);
                
                // Create AI response message
                var fastPathResponse = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = fastPathResult,
                    UserId = "ai-assistant",
                    UserName = "AI Assistant", 
                    Timestamp = DateTime.UtcNow,
                    IsAiResponse = true,
                    SessionId = message.SessionId
                };

                // Save AI response to conversation memory
                await _conversationMemory.AddMessageAsync(fastPathResponse);
                _logger.LogInformation("⏱️ TOTAL REQUEST TIME (Fast-path): {TotalMs}ms", stopwatch.ElapsedMilliseconds);
                return Ok(fastPathResponse);
            }
            _logger.LogInformation("⏱️ Fast-path detection completed in {ElapsedMs}ms - no match, proceeding with AI (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - fastPathStartTime, stopwatch.ElapsedMilliseconds);

            // Enable automatic function calling with optimized execution settings
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 1000, // Reduced for faster responses
                Temperature = 0.0, // More deterministic for card responses
                TopP = 0.1 // More focused responses
            };

            // Get the AI response with automatic function calling enabled
            _logger.LogInformation("⏱️ Starting AI chat completion at {TotalMs}ms", stopwatch.ElapsedMilliseconds);
            var aiStartTime = stopwatch.ElapsedMilliseconds;
            
            var response = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings, 
                userKernel);

            _logger.LogInformation("⏱️ AI chat completion finished in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - aiStartTime, stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Raw AI response content: {Content}", response.Content);
            
            // Log function calls if any were made
            if (response.Metadata?.ContainsKey("FunctionCalls") == true)
            {
                _logger.LogInformation("Function calls were made during this request");
            }
            
            // Log if the response contains function results
            var responseProcessingStartTime = stopwatch.ElapsedMilliseconds;
            foreach (var item in response.Items)
            {
                _logger.LogInformation("Response item type: {ItemType}, Content: {Content}", 
                    item.GetType().Name, item.ToString());
                

            }

            // Check if any function returned card data and use it directly
            string finalContent = response.Content ?? "I'm sorry, I couldn't generate a response.";
            
            // Check function results for card data and override AI response if found
            foreach (var item in response.Items)
            {
                var itemType = item.GetType().Name;
                if (itemType == "FunctionResultContent")
                {
                    // Use reflection to get the Result property
                    var resultProperty = item.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var resultString = resultProperty.GetValue(item)?.ToString() ?? "";
                        if (resultString.StartsWith("TASK_CARDS:") || resultString.StartsWith("CALENDAR_CARDS:"))
                        {
                            _logger.LogInformation("Found card data in function result, using it directly instead of AI interpretation");
                            finalContent = resultString;
                            break; // Use the first card data found
                        }
                    }
                }
            }
            
            // Log if we detect card data (for debugging)
            if (finalContent.Contains("CALENDAR_CARDS:") || finalContent.Contains("TASK_CARDS:"))
            {
                _logger.LogInformation("Response contains card data, passing through as-is");
            }
            _logger.LogInformation("⏱️ Response processing completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - responseProcessingStartTime, stopwatch.ElapsedMilliseconds);

            // Create AI response message
            var messageCreationStartTime = stopwatch.ElapsedMilliseconds;
            var aiResponse = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = finalContent,
                UserId = "ai-assistant",
                UserName = "AI Assistant", 
                Timestamp = DateTime.UtcNow,
                IsAiResponse = true,
                SessionId = message.SessionId
            };

            // Save AI response to conversation memory
            await _conversationMemory.AddMessageAsync(aiResponse);
            _logger.LogInformation("⏱️ AI response creation and save completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - messageCreationStartTime, stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Generated and saved AI response for user {UserId} in session {SessionId}", userId, message.SessionId);
            _logger.LogInformation("⏱️ TOTAL REQUEST TIME: {TotalMs}ms", stopwatch.ElapsedMilliseconds);

            return Ok(aiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message from user {UserId}", message.UserId);
            
            string errorMessage = "I'm experiencing technical difficulties. Please try again later.";
            
            // Provide specific error messages for common issues
            if (ex.Message.Contains("invalid_api_key") || ex.Message.Contains("Incorrect API key"))
            {
                errorMessage = "🔑 **OpenAI API Configuration Error**\n\n" +
                              "The OpenAI API key is missing or invalid. Please check the server configuration.\n\n" +
                              "**For Administrators:**\n" +
                              "• Verify the OpenAI API key in `appsettings.Development.json`\n" +
                              "• Ensure the key starts with `sk-` and is valid\n" +
                              "• Check your OpenAI account has sufficient credits\n\n" +
                              "**Error Details:** Invalid or missing OpenAI API key";
            }
            else if (ex.Message.Contains("insufficient_quota") || ex.Message.Contains("quota"))
            {
                errorMessage = "💳 **OpenAI Quota Exceeded**\n\n" +
                              "The OpenAI API quota has been exceeded. Please check your OpenAI account.\n\n" +
                              "**For Administrators:**\n" +
                              "• Check your OpenAI account billing and usage\n" +
                              "• Add credits to your OpenAI account\n" +
                              "• Verify your usage limits\n\n" +
                              "**Error Details:** OpenAI API quota exceeded";
            }
            else if (ex.Message.Contains("rate_limit") || ex.Message.Contains("Rate limit"))
            {
                errorMessage = "⏱️ **Rate Limit Exceeded**\n\n" +
                              "Too many requests to OpenAI API. Please wait a moment and try again.\n\n" +
                              "**Error Details:** OpenAI API rate limit exceeded";
            }
            else if (ex.Message.Contains("model_not_found") || ex.Message.Contains("model"))
            {
                errorMessage = "🤖 **Model Configuration Error**\n\n" +
                              "The specified OpenAI model is not available or not found.\n\n" +
                              "**For Administrators:**\n" +
                              "• Check the `DeploymentOrModelId` in configuration\n" +
                              "• Verify the model name is correct (e.g., 'gpt-4o-mini')\n" +
                              "• Ensure your OpenAI account has access to this model\n\n" +
                              "**Error Details:** OpenAI model not found or unavailable";
            }
            else if (ex.Message.Contains("network") || ex.Message.Contains("timeout"))
            {
                errorMessage = "🌐 **Network Connection Error**\n\n" +
                              "Unable to connect to OpenAI services. Please check your internet connection.\n\n" +
                              "**Error Details:** Network timeout or connection error";
            }
            
            return StatusCode(500, new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = errorMessage,
                UserId = "ai-assistant",
                UserName = "AI Assistant",
                Timestamp = DateTime.UtcNow,
                IsAiResponse = true,
                SessionId = message.SessionId
            });
        }
    }

    private Kernel CreateUserContextKernel(string userAccessToken, string userId, string userName)
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("SemanticKernel").Get<SemanticKernelConfig>()
            ?? throw new InvalidOperationException("SemanticKernel configuration is missing");

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
        if (!string.IsNullOrEmpty(userAccessToken))
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
        if (!string.IsNullOrEmpty(userAccessToken))
        {
            kernel.Data["UserAccessToken"] = userAccessToken;
            kernel.Data["UserId"] = userId ?? string.Empty;
            kernel.Data["UserName"] = userName ?? string.Empty;
            
            // Log available functions for debugging
            var functions = kernel.Plugins.GetFunctionsMetadata();
            _logger.LogInformation("Available functions for user {UserId}: {Functions}", 
                userId, string.Join(", ", functions.Select(f => $"{f.PluginName}.{f.Name}")));
        }

        return kernel;
    }

    private async Task<string> TryFastPathResponse(string userMessage, Kernel kernel)
    {
        var messageLower = userMessage.ToLower().Trim();
        _logger.LogInformation("🚀 Fast-path detection for message: '{Message}'", userMessage);
        
        // Email/Mail fast-path (check this FIRST)
        if (messageLower.Contains("mail") || messageLower.Contains("email") || 
            messageLower.Contains("inbox") || messageLower.Contains("message"))
        {
            _logger.LogInformation("📧 Fast-path: Detected EMAIL request");
            try
            {
                var mailPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "MailPlugin");
                if (mailPlugin != null)
                {
                    var getRecentEmailsFunction = mailPlugin.FirstOrDefault(f => f.Name == "GetRecentEmails");
                    if (getRecentEmailsFunction != null)
                    {
                        _logger.LogInformation("📧 Fast-path: Calling GetRecentEmails");
                        var result = await kernel.InvokeAsync(getRecentEmailsFunction);
                        return result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fast-path email function call failed, will fall back to AI");
                return null; // Fall back to AI processing
            }
        }
        
        // Calendar/Appointments fast-path (check this SECOND to avoid conflicts)
        if (messageLower.Contains("appointment") || messageLower.Contains("calendar") || 
            messageLower.Contains("meeting") || messageLower.Contains("event") ||
            messageLower.Contains("future") || messageLower.Contains("upcoming") ||
            (messageLower.Contains("today") && (messageLower.Contains("schedule") || messageLower.Contains("event"))) ||
            (messageLower.Contains("others") && messageLower.Contains("future")) ||
            (messageLower.Contains("any") && (messageLower.Contains("appointment") || messageLower.Contains("meeting") || messageLower.Contains("event"))))
        {
            _logger.LogInformation("🗓️ Fast-path: Detected CALENDAR request");
            try
            {
                var calendarPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "CalendarPlugin");
                if (calendarPlugin != null)
                {
                    // Determine which calendar function to call
                    if (messageLower.Contains("today"))
                    {
                        _logger.LogInformation("🗓️ Fast-path: Calling GetTodaysEvents");
                        var getTodaysEventsFunction = calendarPlugin.FirstOrDefault(f => f.Name == "GetTodaysEvents");
                        if (getTodaysEventsFunction != null)
                        {
                            var result = await kernel.InvokeAsync(getTodaysEventsFunction);
                            return result.ToString();
                        }
                    }
                    else if (messageLower.Contains("upcoming") || messageLower.Contains("future") || 
                             (messageLower.Contains("others") && messageLower.Contains("future")))
                    {
                        _logger.LogInformation("🗓️ Fast-path: Calling GetUpcomingEvents (upcoming/future)");
                        var getUpcomingEventsFunction = calendarPlugin.FirstOrDefault(f => f.Name == "GetUpcomingEvents");
                        if (getUpcomingEventsFunction != null)
                        {
                            // For "future" requests, look ahead more days (30 instead of default 7)
                            var arguments = new KernelArguments
                            {
                                ["days"] = 30,
                                ["maxEvents"] = 20
                            };
                            var result = await kernel.InvokeAsync(getUpcomingEventsFunction, arguments);
                            return result.ToString();
                        }
                    }
                    else
                    {
                        _logger.LogInformation("🗓️ Fast-path: Calling GetUpcomingEvents (default)");
                        // Default to upcoming events for general appointment requests
                        var getUpcomingEventsFunction = calendarPlugin.FirstOrDefault(f => f.Name == "GetUpcomingEvents");
                        if (getUpcomingEventsFunction != null)
                        {
                            var result = await kernel.InvokeAsync(getUpcomingEventsFunction);
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fast-path calendar function call failed, will fall back to AI");
                return null; // Fall back to AI processing
            }
        }
        
        // Tasks/Notes fast-path (check LAST to avoid conflicts with email/calendar)
        if ((messageLower.Contains("task") || messageLower.Contains("todo") || messageLower.Contains("todos") ||
            (messageLower.Contains("my tasks")) || 
            (messageLower.Contains("show me my") && (messageLower.Contains("task") || messageLower.Contains("todo")))) &&
            !messageLower.Contains("appointment") && !messageLower.Contains("calendar") && 
            !messageLower.Contains("meeting") && !messageLower.Contains("event") &&
            !messageLower.Contains("mail") && !messageLower.Contains("email") && 
            !messageLower.Contains("inbox") && !messageLower.Contains("message"))
        {
            _logger.LogInformation("📝 Fast-path: Detected TASKS/TODOS request");
            try
            {
                var todoPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "ToDoPlugin");
                if (todoPlugin != null)
                {
                    var getRecentNotesFunction = todoPlugin.FirstOrDefault(f => f.Name == "GetRecentNotes");
                    if (getRecentNotesFunction != null)
                    {
                        _logger.LogInformation("📝 Fast-path: Calling GetRecentNotes");
                        var result = await kernel.InvokeAsync(getRecentNotesFunction);
                        return result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fast-path task function call failed, will fall back to AI");
                return null; // Fall back to AI processing
            }
        }
        
        _logger.LogInformation("🤖 Fast-path: No match found, will use AI processing");
        return null; // No fast-path match, use AI processing
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