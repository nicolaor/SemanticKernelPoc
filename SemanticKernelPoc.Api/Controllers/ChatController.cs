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
using SemanticKernelPoc.Api.Plugins;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;
using SemanticKernelPoc.Api.Services.Intelligence;
using SemanticKernelPoc.Api.Services.Workflows;


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
    private readonly IConversationContextService _conversationContext;
    private readonly ISmartFunctionSelector _functionSelector;
    private readonly IWorkflowOrchestrator _workflowOrchestrator;
    private readonly IProcessFrameworkOrchestrator _processFrameworkOrchestrator;


    public ChatController(
        ILogger<ChatController> logger,
        Kernel kernel,
        ITokenAcquisition tokenAcquisition,
        IConversationMemoryService conversationMemory,
        IConversationContextService conversationContext,
        ISmartFunctionSelector functionSelector,
        IWorkflowOrchestrator workflowOrchestrator,
        IProcessFrameworkOrchestrator processFrameworkOrchestrator)
    {
        _logger = logger;
        _kernel = kernel;
        _tokenAcquisition = tokenAcquisition;
        _conversationMemory = conversationMemory;
        _conversationContext = conversationContext;
        _functionSelector = functionSelector;
        _workflowOrchestrator = workflowOrchestrator;
        _processFrameworkOrchestrator = processFrameworkOrchestrator;
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

            // Get or create conversation context
            var conversationContext = await _conversationContext.GetConversationContextAsync(message.SessionId);
            conversationContext.UserId = userId ?? "";

            // Check for workflow triggers before processing normally (using Process Framework)
            var workflowTrigger = await _processFrameworkOrchestrator.DetectWorkflowTriggerAsync(message.Content, conversationContext);
            
            if (!string.IsNullOrEmpty(workflowTrigger.WorkflowDefinitionId))
            {
                _logger.LogInformation("Workflow trigger detected: {WorkflowId} for user {UserId}", 
                    workflowTrigger.WorkflowDefinitionId, userId);
                
                // Get the available processes
                var availableProcesses = await _processFrameworkOrchestrator.GetAvailableProcessesAsync();
                var processDefinition = availableProcesses.FirstOrDefault(p => p.Id == workflowTrigger.WorkflowDefinitionId);
                
                if (processDefinition != null)
                {
                    // Get user's Microsoft Graph token for workflow execution
                    string workflowUserAccessToken = null;
                    try
                    {
                        workflowUserAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                            scopes: new[] { "https://graph.microsoft.com/.default" },
                            user: User);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not acquire Microsoft Graph token for workflow execution");
                        return StatusCode(500, new ChatMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            Content = "I need access to your Microsoft 365 data to execute this workflow. Please ensure you're properly authenticated.",
                            UserId = "ai-assistant",
                            UserName = "AI Assistant",
                            Timestamp = DateTime.UtcNow,
                            IsAiResponse = true,
                            SessionId = message.SessionId
                        });
                    }

                    // Create a full kernel for workflow execution
                    var workflowKernel = CreateUserContextKernel(workflowUserAccessToken, userId, userName);
                    
                    // Execute the process using Process Framework
                    var processInputs = new Dictionary<string, object>
                    {
                        ["userMessage"] = message.Content,
                        ["context"] = conversationContext
                    };
                    
                    var processExecution = await _processFrameworkOrchestrator.ExecuteProcessAsync(
                        processDefinition.Id, processInputs);
                    
                    // Generate response based on process execution
                    var workflowResponse = GenerateProcessResponse(processExecution, processDefinition);
                    
                    // Create AI response message
                    var workflowAiResponse = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Content = workflowResponse,
                        UserId = "ai-assistant",
                        UserName = "AI Assistant",
                        Timestamp = DateTime.UtcNow,
                        IsAiResponse = true,
                        SessionId = message.SessionId,
                        WorkflowId = processDefinition.Id
                    };

                    // Save AI response to conversation memory
                    await _conversationMemory.AddMessageAsync(workflowAiResponse);
                    
                    _logger.LogInformation("Process execution completed for user {UserId}: {Status}", 
                        userId, processExecution.IsSuccess ? "Success" : "Failed");
                    
                    return Ok(workflowAiResponse);
                }
            }

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

            // Get all available functions for smart selection
            var allFunctions = userKernel.Plugins.GetFunctionsMetadata();
            
            // Use smart function selection to determine relevant functions
            var functionSelection = _functionSelector.SelectRelevantFunctions(
                message.Content, conversationContext, allFunctions);

            _logger.LogInformation("Smart function selection: {SelectionReason}", functionSelection.SelectionReason);

            // Create a filtered kernel with only relevant functions
            var filteredKernel = CreateFilteredKernel(userKernel, functionSelection.SelectedFunctions);

            // Get the chat completion service
            var chatCompletionService = filteredKernel.GetRequiredService<IChatCompletionService>();
            
            // Load conversation history and create chat history
            var conversationHistory = await _conversationMemory.GetConversationHistoryAsync(message.SessionId, maxMessages: 20);
            var chatHistory = new ChatHistory();
            
            // Build contextual system message
            var systemMessage = BuildContextualSystemMessage(userName, conversationContext, functionSelection);
            chatHistory.AddSystemMessage(systemMessage);
            
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
                Temperature = 0.0  // Set to 0 for maximum determinism in following instructions
            };

            // Get the AI response with automatic function calling enabled
            var response = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings, 
                filteredKernel);

            // Extract called functions for context update
            var calledFunctions = ExtractCalledFunctions(response);

            // Update conversation context with the interaction
            _functionSelector.UpdateConversationContext(
                conversationContext, message.Content, response.Content ?? "", calledFunctions);
            
            // Save updated context
            await _conversationContext.UpdateConversationContextAsync(conversationContext);

            _logger.LogInformation("Raw AI response content: {Content}", response.Content);

            // Use the response content as-is - the plugins already return properly formatted card data
            string finalContent = response.Content ?? "I'm sorry, I couldn't generate a response.";
            
            // Log if we detect card data (for debugging)
            if (finalContent.Contains("CALENDAR_CARDS:") || finalContent.Contains("NOTE_CARDS:"))
            {
                _logger.LogInformation("✅ Response contains card data, passing through as-is");
            }
            else if (calledFunctions.Any(f => f.Contains("Calendar") || f.Contains("ToDo")))
            {
                _logger.LogWarning("⚠️ Calendar/ToDo function was called but response doesn't contain card data. Functions called: {Functions}", string.Join(", ", calledFunctions));
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
            
            // Create a temporary kernel for the MeetingPlugin (it needs access to the kernel for AI operations)
            var tempKernel = kernelBuilder.Build();
            var meetingPlugin = new MeetingPlugin(graphService.CreateClient(userAccessToken), tempKernel);
            
            kernelBuilder.Plugins.AddFromObject(new SharePointPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<SharePointPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(new OneDrivePlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>()));
            kernelBuilder.Plugins.AddFromObject(calendarPlugin);
            kernelBuilder.Plugins.AddFromObject(new MailPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>()));
            kernelBuilder.Plugins.AddFromObject(todoPlugin);
            kernelBuilder.Plugins.AddFromObject(meetingPlugin);
            _logger.LogInformation("Added SharePoint, OneDrive, Calendar, Mail, ToDo, and Meeting plugins for user {UserId}", userId);
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

    private Kernel CreateFilteredKernel(Kernel sourceKernel, IEnumerable<string> selectedFunctions)
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("SemanticKernel").Get<SemanticKernelConfig>()
            ?? throw new InvalidOperationException("SemanticKernel configuration is missing");

        var kernelBuilder = Kernel.CreateBuilder();
        
        // Add the AI service (same as source kernel)
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

        var filteredKernel = kernelBuilder.Build();

        // Copy user context data
        foreach (var (key, value) in sourceKernel.Data)
        {
            filteredKernel.Data[key] = value;
        }

        // Add only selected plugins/functions
        var userAccessToken = sourceKernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
        if (!string.IsNullOrEmpty(userAccessToken))
        {
            var graphService = HttpContext.RequestServices.GetRequiredService<IGraphService>();
            
            // Only add plugins that have selected functions
            var selectedPlugins = selectedFunctions.Select(f => f.Split('.')[0]).Distinct();
            
            foreach (var pluginName in selectedPlugins)
            {
                switch (pluginName.ToLowerInvariant())
                {
                    case "calendarplugin":
                        var calendarPlugin = new CalendarPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<CalendarPlugin>>());
                        filteredKernel.Plugins.AddFromObject(calendarPlugin);
                        break;
                    case "todoplugin":
                        var todoPlugin = new ToDoPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<ToDoPlugin>>());
                        filteredKernel.Plugins.AddFromObject(todoPlugin);
                        break;
                    case "mailplugin":
                        var mailPlugin = new MailPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<MailPlugin>>());
                        filteredKernel.Plugins.AddFromObject(mailPlugin);
                        break;
                    case "sharepointplugin":
                        var sharePointPlugin = new SharePointPlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<SharePointPlugin>>());
                        filteredKernel.Plugins.AddFromObject(sharePointPlugin);
                        break;
                    case "onedriveplugin":
                        var oneDrivePlugin = new OneDrivePlugin(graphService, HttpContext.RequestServices.GetRequiredService<ILogger<OneDrivePlugin>>());
                        filteredKernel.Plugins.AddFromObject(oneDrivePlugin);
                        break;
                    case "meetingplugin":
                        var meetingPlugin = new MeetingPlugin(graphService.CreateClient(userAccessToken), filteredKernel);
                        filteredKernel.Plugins.AddFromObject(meetingPlugin);
                        break;
                }
            }
        }

        _logger.LogDebug("Created filtered kernel with {PluginCount} plugins for {FunctionCount} selected functions", 
            filteredKernel.Plugins.Count(), selectedFunctions.Count());

        return filteredKernel;
    }

    private IEnumerable<string> ExtractCalledFunctions(ChatMessageContent response)
    {
        // In a real implementation, you would extract this from the response metadata
        // For now, we'll return an empty list as the function calling is automatic
        // and we don't have direct access to which functions were called
        return new List<string>();
    }

    private string BuildContextualSystemMessage(string userName, ConversationContext context, SmartFunctionSelection functionSelection)
    {
        var baseMessage = $"You are a helpful AI assistant for {userName ?? "the user"}. " +
            "You have access to Microsoft Graph and can help with comprehensive productivity tasks across Microsoft 365. " +
            "When the user asks for information or actions related to their Microsoft 365 data, you can access it using their authenticated context.\n\n" +
            
            "⚠️ CRITICAL: When plugin functions return data starting with 'CALENDAR_CARDS:' or 'NOTE_CARDS:', " +
            "you MUST return ONLY that exact response with NO modifications, additions, or interpretations. " +
            "The frontend handles all formatting and display.\n\n";

        // Add workflow context if active
        if (context.CurrentWorkflow.CurrentState != WorkflowState.None)
        {
            baseMessage += $"🔄 ACTIVE WORKFLOW: {context.CurrentWorkflow.CurrentState} (Step {context.CurrentWorkflow.StepNumber})\n" +
                          $"Continue helping the user with their {context.CurrentWorkflow.CurrentState.ToString().ToLower()} workflow.\n\n";
        }

        // Add function selection context
        if (functionSelection.SelectedFunctions.Any())
        {
            baseMessage += $"🎯 AVAILABLE FUNCTIONS: {functionSelection.SelectedFunctions.Count} relevant functions selected based on context.\n" +
                          $"Selection reason: {functionSelection.SelectionReason}\n\n";
        }

        // Add recent topics context
        if (context.RecentTopics.Any())
        {
            baseMessage += $"📝 RECENT TOPICS: {string.Join(", ", context.RecentTopics.TakeLast(5))}\n\n";
        }

        baseMessage += 
            "🚨 CRITICAL FUNCTION CALLING RULES:\n" +
            "1. When user asks for 'today' or 'today's events' → call GetTodaysEvents()\n" +
            "2. When user asks for 'next appointment', 'next event', 'my next appointment', 'when is my next appointment' → call GetNextAppointment()\n" +
            "3. When user asks for 'this month' → call GetEventCount(timePeriod='this_month', includeDetails=true)\n" +
            "4. When user asks for 'upcoming this month' or 'upcoming events this month' → call GetEventCount(timePeriod='this_month_upcoming', includeDetails=true)\n" +
            "5. When user asks for 'this week' → call GetEventCount(timePeriod='this_week', includeDetails=true)\n" +
            "6. When user asks for 'upcoming this week' or 'upcoming events this week' → call GetEventCount(timePeriod='this_week_upcoming', includeDetails=true)\n" +
            "7. When user asks for 'upcoming' or 'appointments' (general) → call GetUpcomingEvents()\n" +
            "8. When user asks for 'notes', 'my notes', 'show notes' → call GetRecentNotes()\n" +
            "9. When user asks for 'tasks', 'my tasks', 'todo', 'assigned to me', 'task list', 'what tasks' → call GetRecentNotes()\n" +
            "10. When functions return 'CALENDAR_CARDS:' or 'NOTE_CARDS:' → IMMEDIATELY return ONLY that exact response with ZERO additional text, formatting, or interpretation\n" +
            "11. NEVER provide manual responses for calendar, note, or task data - ALWAYS use functions first\n\n" +
            
            "🎯 CONVERSATIONAL APPROACH:\n" +
            "• ALWAYS use a step-by-step conversational approach when information is missing\n" +
            "• Ask for ONE piece of missing information at a time\n" +
            "• Only call functions when you have ALL required parameters\n" +
            "• Be conversational and friendly in your requests for information\n" +
            "• Remember previous answers in the conversation to avoid re-asking\n" +
            "• ⚠️ CRITICAL: When calling creation functions (CreateNote, AddCalendarEvent), WAIT for the function to complete and return the result before responding\n" +
            "• ✅ CREATION FLOW: For creation requests, call the function and return ONLY the function's response (which will be card data)\n" +
            "• 🚫 NEVER say 'I'll create...' and then ask for something else - complete the creation first\n\n" +
            
            "🔹 CALENDAR CAPABILITIES:\n" +
            "• View upcoming events and today's schedule (GetUpcomingEvents, GetTodaysEvents)\n" +
            "• Get EVENT COUNTS for specific time periods (GetEventCount)\n" +
            "• Add new calendar events with attendees, locations, and descriptions (AddCalendarEvent - returns CALENDAR_CARDS showing the new event)\n" +
            "• Get events in specific date ranges (GetEventsInDateRange)\n" +
            "• CREATION FEEDBACK: AddCalendarEvent function automatically shows the newly created event as a card with a 'Just Created' indicator\n\n" +
            
            "🔹 NOTE-TAKING & TASK CAPABILITIES:\n" +
            "• Create notes/tasks - when user says 'create a note' or 'create task', use CreateNote function (returns NOTE_CARDS showing the new task)\n" +
            "• Get recent notes/tasks - ALWAYS use GetRecentNotes function when user asks for notes, tasks, todo, or assigned items\n" +
            "• Search notes/tasks - ALWAYS use SearchNotes function when user wants to find specific notes or tasks\n" +
            "• Mark notes/tasks as complete or update their status\n" +
            "• IMPORTANT: Notes and tasks are the same thing in this system - both use ToDoPlugin functions\n" +
            "• CREATION FEEDBACK: CreateNote function automatically shows the newly created task as a card with a 'Just Created' indicator\n\n" +
            
            "🔹 EMAIL CAPABILITIES:\n" +
            "• Read recent emails with previews and metadata\n" +
            "• Send emails immediately with CC support and importance levels\n" +
            "• Search emails by subject, sender, or content\n\n" +
            
            "🔹 SHAREPOINT CAPABILITIES:\n" +
            "• Browse and search SharePoint sites the user has access to\n" +
            "• View site details, document libraries, and file information\n" +
            "• Search for files across SharePoint with various filters\n\n" +
            
            "🔹 MEETING TRANSCRIPT CAPABILITIES:\n" +
            "• Access recent Teams meeting transcripts (GetMeetingTranscripts)\n" +
            "• Get full transcript content for specific meetings (GetMeetingTranscript)\n" +
            "• Generate AI-powered meeting summaries (SummarizeMeeting)\n" +
            "• Extract key decisions and action items (ExtractKeyDecisions)\n" +
            "• Propose actionable tasks from meeting content (ProposeTasksFromMeeting)\n" +
            "• Create Microsoft To Do tasks from proposals (CreateTasksFromProposals)\n\n" +
            
            "🚨 CRITICAL INSTRUCTIONS - FOLLOW EXACTLY:\n" +
            "1. When ANY calendar plugin function returns data that starts with 'CALENDAR_CARDS:', you MUST return ONLY that exact response with NO additional text, formatting, or interpretation.\n" +
            "2. When ANY note/todo plugin function returns data that starts with 'NOTE_CARDS:', you MUST return ONLY that exact response with NO additional text, formatting, or interpretation.\n" +
            "3. For calendar-related requests, ONLY call calendar functions and return their responses directly - DO NOT interpret or summarize the data.\n" +
            "4. For note-related requests, ONLY call note functions and return their responses directly - DO NOT interpret or summarize the data.\n" +
            "5. NEVER generate partial CALENDAR_CARDS or NOTE_CARDS responses manually.\n" +
            "6. NEVER add introductory text like 'Here are your events' before CALENDAR_CARDS responses.\n" +
            "7. NEVER add explanatory text after CALENDAR_CARDS responses.\n" +
            "8. The frontend will handle all formatting and display of card data - your job is ONLY to return the raw card data.\n" +
            "9. ✨ CREATION FEEDBACK: When users create items (notes, tasks, calendar events), the functions automatically return card data showing the newly created items with special 'Just Created' indicators.\n" +
            "10. 🎯 USER EXPERIENCE: Always provide immediate visual feedback for creation actions - users will see their newly created items as cards, not text confirmations.\n\n" +
            
            "Always be respectful of privacy and only access what is needed to fulfill requests. " +
            "Provide clear, helpful responses with actionable information. " +
            "Remember: Use functions for data retrieval, be conversational, and return card data exactly as provided by functions.";

        return baseMessage;
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

    private string GenerateWorkflowResponse(WorkflowExecution execution, WorkflowDefinition definition)
    {
        var response = $"🔄 **Workflow Executed: {definition.Name}**\n\n";
        response += $"📋 **Description:** {definition.Description}\n";
        response += $"⏱️ **Status:** {execution.Status}\n";
        response += $"🕐 **Started:** {execution.StartedAt:HH:mm:ss}\n";
        
        if (execution.CompletedAt.HasValue)
        {
            var duration = execution.CompletedAt.Value - execution.StartedAt;
            response += $"✅ **Completed:** {execution.CompletedAt.Value:HH:mm:ss} (took {duration.TotalSeconds:F1}s)\n";
        }

        response += $"\n📊 **Step Results:**\n";
        
        foreach (var stepExecution in execution.StepExecutions.OrderBy(s => s.StartedAt))
        {
            var statusIcon = stepExecution.Status switch
            {
                WorkflowStepStatus.Completed => "✅",
                WorkflowStepStatus.Failed => "❌",
                WorkflowStepStatus.Skipped => "⏭️",
                WorkflowStepStatus.Running => "🔄",
                WorkflowStepStatus.Retrying => "🔁",
                _ => "⏸️"
            };
            
            response += $"{statusIcon} **{stepExecution.StepName}**: {stepExecution.Status}";
            
            if (stepExecution.ExecutionTime.HasValue)
            {
                response += $" ({stepExecution.ExecutionTime.Value.TotalMilliseconds:F0}ms)";
            }
            
            if (!string.IsNullOrEmpty(stepExecution.ErrorMessage))
            {
                response += $"\n   ⚠️ Error: {stepExecution.ErrorMessage}";
            }
            
            response += "\n";
        }

        // Include final outputs if available
        if (execution.FinalOutputs.Any())
        {
            response += "\n🎯 **Results:**\n";
            foreach (var output in execution.FinalOutputs.Take(3)) // Limit to first 3 outputs
            {
                response += $"• **{output.Key}**: {output.Value}\n";
            }
        }

        // Add error message if workflow failed
        if (execution.Status == WorkflowExecutionStatus.Failed && !string.IsNullOrEmpty(execution.ErrorMessage))
        {
            response += $"\n❌ **Error:** {execution.ErrorMessage}\n";
        }

        // Add success message for completed workflows
        if (execution.Status == WorkflowExecutionStatus.Completed)
        {
            response += "\n🎉 **Workflow completed successfully!** All steps executed as planned.";
        }
        else if (execution.Status == WorkflowExecutionStatus.PartiallyCompleted)
        {
            response += "\n⚠️ **Workflow partially completed.** Some steps succeeded while others failed.";
        }

        return response;
    }

    private string GenerateProcessResponse(ProcessExecutionResult execution, WorkflowDefinition definition)
    {
        var response = $"🔄 **Process Executed: {definition.Name}**\n\n";
        response += $"📋 **Description:** {definition.Description}\n";
        response += $"⏱️ **Status:** {(execution.IsSuccess ? "Completed" : "Failed")}\n";
        response += $"🕐 **Duration:** {execution.Duration.TotalSeconds:F1}s\n";

        if (execution.IsSuccess)
        {
            response += "\n🎉 **Process completed successfully!** All steps executed using the Semantic Kernel Process Framework.";
            
            if (execution.Outputs.Any())
            {
                response += "\n\n🎯 **Results:**\n";
                foreach (var output in execution.Outputs.Take(3))
                {
                    response += $"• **{output.Key}**: {output.Value}\n";
                }
            }
        }
        else
        {
            response += $"\n❌ **Error:** {execution.ErrorMessage}\n";
        }

        return response;
    }
} 