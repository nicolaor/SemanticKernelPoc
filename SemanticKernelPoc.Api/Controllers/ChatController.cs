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
        try
        {
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
            
            _logger.LogInformation("Received chat message from user {UserId} ({UserName}) in session {SessionId}: {Content}", 
                userId, userName, message.SessionId, message.Content);

            // Save user message to conversation memory
            await _conversationMemory.AddMessageAsync(message);

            // Get user's Microsoft Graph token for user-context operations
            string userAccessToken = null;
            try
            {
                userAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                    scopes: new[] { "https://graph.microsoft.com/.default" },
                    user: User);
                _logger.LogInformation("Successfully acquired Microsoft Graph token for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not acquire Microsoft Graph token for user {UserId}. Continuing without user context.", userId);
            }

            // Create a user-context kernel with the user's token
            var userKernel = CreateUserContextKernel(userAccessToken, userId, userName);

            // Get the chat completion service
            var chatCompletionService = userKernel.GetRequiredService<IChatCompletionService>();
            
            // Load conversation history and create chat history
            var conversationHistory = await _conversationMemory.GetConversationHistoryAsync(message.SessionId, maxMessages: 20);
            var chatHistory = new ChatHistory();
            
            // Add system message
            chatHistory.AddSystemMessage(
                $"You are a helpful AI assistant for {userName ?? "the user"}. " +
                "You have access to Microsoft Graph and can help with comprehensive productivity tasks across Microsoft 365. " +
                "When the user asks for information or actions related to their Microsoft 365 data, you can access it using their authenticated context. " +
                
                "🚨 CRITICAL FUNCTION CALLING RULES:\n" +
                "1. When user asks for 'today' or 'today's events' → call GetTodaysEvents()\n" +
                "2. When user asks for 'this month' → call GetEventCount(timePeriod='this_month', includeDetails=true)\n" +
                "3. When user asks for 'upcoming this month' or 'upcoming events this month' → call GetEventCount(timePeriod='this_month_upcoming', includeDetails=true)\n" +
                "4. When user asks for 'this week' → call GetEventCount(timePeriod='this_week', includeDetails=true)\n" +
                "5. When user asks for 'upcoming this week' or 'upcoming events this week' → call GetEventCount(timePeriod='this_week_upcoming', includeDetails=true)\n" +
                "6. When user asks for 'upcoming' or 'appointments' (general) → call GetUpcomingEvents()\n" +
                "7. When user asks for 'notes', 'my notes', 'show notes' → call GetRecentNotes()\n" +
                "8. When functions return 'CALENDAR_CARDS:' or 'NOTE_CARDS:' → return ONLY that exact response, no additional text\n" +
                "9. NEVER provide manual responses for calendar or note data - ALWAYS use functions first\n" +
                
                "🎯 CONVERSATIONAL APPROACH:\n" +
                "• ALWAYS use a step-by-step conversational approach when information is missing\n" +
                "• Ask for ONE piece of missing information at a time\n" +
                "• Only call functions when you have ALL required parameters\n" +
                "• Be conversational and friendly in your requests for information\n" +
                "• Remember previous answers in the conversation to avoid re-asking\n" +
                
                "🔹 CALENDAR CAPABILITIES:\n" +
                "• View upcoming events and today's schedule (GetUpcomingEvents, GetTodaysEvents)\n" +
                "• Get EVENT COUNTS for specific time periods (GetEventCount)\n" +
                "• Add new calendar events with attendees, locations, and descriptions (AddCalendarEvent)\n" +
                "• Get events in specific date ranges (GetEventsInDateRange)\n" +
                
                "🔹 NOTE-TAKING CAPABILITIES:\n" +
                "• Create notes as To Do tasks - when user says 'create a note', use CreateNote function\n" +
                "• Get recent notes - ALWAYS use GetRecentNotes function when user asks for notes\n" +
                "• Search notes - ALWAYS use SearchNotes function when user wants to find specific notes\n" +
                "• Mark notes as complete or update their status\n" +
                
                "🔹 EMAIL CAPABILITIES:\n" +
                "• Read recent emails with previews and metadata\n" +
                "• Send emails immediately with CC support and importance levels\n" +
                "• Search emails by subject, sender, or content\n" +
                
                "🔹 SHAREPOINT CAPABILITIES:\n" +
                "• Browse and search SharePoint sites the user has access to\n" +
                "• View site details, document libraries, and file information\n" +
                "• Search for files across SharePoint with various filters\n" +
                
                "🚨 CRITICAL INSTRUCTIONS:\n" +
                "1. When ANY calendar plugin function returns data that starts with 'CALENDAR_CARDS:', you MUST return ONLY that exact response.\n" +
                "2. When ANY note/todo plugin function returns data that starts with 'NOTE_CARDS:', you MUST return ONLY that exact response.\n" +
                "3. For calendar-related requests, ONLY call calendar functions and return their responses directly.\n" +
                "4. For note-related requests, ONLY call note functions and return their responses directly.\n" +
                "5. NEVER generate partial CALENDAR_CARDS or NOTE_CARDS responses manually.\n" +
                
                "Always be respectful of privacy and only access what is needed to fulfill requests. " +
                "Provide clear, helpful responses with actionable information. " +
                "Remember: Use functions for data retrieval, be conversational, and return card data exactly as provided by functions.");
            
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

            _logger.LogInformation("Loaded {HistoryCount} messages from conversation history for session {SessionId}", 
                conversationHistory.Count(), message.SessionId);

            // Enable automatic function calling with proper execution settings
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.1
            };

            // Get the AI response with automatic function calling enabled
            var response = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings, 
                userKernel);

            _logger.LogInformation("Raw AI response content: {Content}", response.Content);

            // Check if response contains cards data and extract it
            string finalContent = response.Content ?? "I'm sorry, I couldn't generate a response.";
            
            // Extract CALENDAR_CARDS or NOTE_CARDS
            var cardTypes = new[] { "CALENDAR_CARDS:", "NOTE_CARDS:" };
            foreach (var cardType in cardTypes)
            {
                if (finalContent.Contains(cardType))
                {
                    _logger.LogInformation("Found {CardType} in response, extracting...", cardType);
                    
                    // Find the start of the cards
                    var startIndex = finalContent.IndexOf(cardType);
                    if (startIndex >= 0)
                    {
                        // Extract everything from cards marker to the end, or until we find text that's clearly not JSON
                        var cardsPart = finalContent.Substring(startIndex);
                        
                        // Try to find where the JSON ends by looking for common JSON ending patterns
                        var jsonEndMarkers = new[] { "}\n\n", "}\r\n\r\n", "}." };
                        var endIndex = -1;
                        
                        foreach (var marker in jsonEndMarkers)
                        {
                            var foundIndex = cardsPart.IndexOf(marker);
                            if (foundIndex > 0)
                            {
                                endIndex = foundIndex + 1; // Include the closing brace
                                break;
                            }
                        }
                        
                        if (endIndex > 0)
                        {
                            finalContent = cardsPart.Substring(0, endIndex);
                        }
                        else
                        {
                            // No clear end marker, try to extract until end or until non-JSON content
                            var lines = cardsPart.Split('\n');
                            var jsonLines = new List<string>();
                            
                            foreach (var line in lines)
                            {
                                jsonLines.Add(line);
                                // Stop if we hit a line that looks like plain text (not JSON)
                                if (line.Trim().Length > 0 && 
                                    !line.TrimStart().StartsWith("{") && 
                                    !line.TrimStart().StartsWith("}") && 
                                    !line.TrimStart().StartsWith("\"") && 
                                    !line.Contains("CALENDAR_CARDS:") &&
                                    !line.Contains("NOTE_CARDS:") &&
                                    !line.Trim().EndsWith(",") &&
                                    !line.Trim().EndsWith("{") &&
                                    !line.Trim().EndsWith("}"))
                                {
                                    jsonLines.RemoveAt(jsonLines.Count - 1); // Remove the non-JSON line
                                    break;
                                }
                            }
                            
                            finalContent = string.Join("\n", jsonLines);
                        }
                        
                        _logger.LogInformation("Extracted {CardType}: {ExtractedContent}", cardType, finalContent);
                        break; // Exit the loop once we find and process cards
                    }
                }
            }

            // Create AI response message
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

            _logger.LogInformation("Generated and saved AI response for user {UserId} in session {SessionId}", userId, message.SessionId);

            return Ok(aiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message from user {UserId}", message.UserId);
            
            return StatusCode(500, new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = "I'm experiencing technical difficulties. Please try again later.",
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
            
            kernelBuilder.Plugins.AddFromObject(new SharePointPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<SharePointPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(new OneDrivePlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>()));
            kernelBuilder.Plugins.AddFromObject(calendarPlugin);
            kernelBuilder.Plugins.AddFromObject(new MailPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(todoPlugin);
            _logger.LogInformation("Added SharePoint, OneDrive, Calendar, Mail, and ToDo plugins for user {UserId}", userId);
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