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
            
            // *** NEW: CHECK FOR SHAREPOINT STRUCTURED DATA IN AI RESPONSE ***
            // Check if the AI response contains SharePoint structured data from MCP tools
            _logger.LogInformation("🔍 Checking AI response for SharePoint structured data...");
            _logger.LogInformation("🔍 AI Response Preview: '{Preview}'", aiResponse.Length > 100 ? aiResponse.Substring(0, 100) + "..." : aiResponse);
            _logger.LogInformation("🔍 Looking for '__sharepoint_structured_data' in response...");
            
            if (aiResponse.Contains("__sharepoint_structured_data"))
            {
                _logger.LogInformation("🔍 Detected SharePoint structured data in AI response, processing...");
                
                try
                {
                    var jsonDoc = JsonDocument.Parse(aiResponse);
                    if (jsonDoc.RootElement.TryGetProperty("__sharepoint_structured_data", out var structuredFlag) && 
                        structuredFlag.GetBoolean() &&
                        jsonDoc.RootElement.TryGetProperty("sites", out var sitesElement))
                    {
                        _logger.LogInformation("✅ Found valid SharePoint structured data, converting to cards...");
                        
                        var sites = JsonSerializer.Deserialize<object[]>(sitesElement.GetRawText());
                        var message = jsonDoc.RootElement.TryGetProperty("message", out var messageElement) 
                            ? messageElement.GetString() : $"Found {sites.Length} SharePoint sites.";
                        var totalCount = jsonDoc.RootElement.TryGetProperty("totalCount", out var countElement) 
                            ? countElement.GetInt32() : sites.Length;
                        
                        // Set structured data in kernel for card display
                        kernel.Data["HasStructuredData"] = "true";
                        kernel.Data["StructuredDataType"] = "sharepoint";
                        kernel.Data["StructuredDataCount"] = sites.Length;
                        kernel.Data["SharePointCards"] = sites;
                        kernel.Data["SharePointFunctionResponse"] = message;
                        
                        _logger.LogInformation("🎯 Successfully set SharePoint card data: {Count} sites", sites.Length);
                        
                        // Override the AI response with a clean message
                        aiResponse = message ?? $"Found {sites.Length} SharePoint sites.";
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to parse SharePoint structured data from AI response");
                }
            }
            else
            {
                _logger.LogInformation("❌ No '__sharepoint_structured_data' found in AI response");
                _logger.LogInformation("🔍 AI Response contains: {Contains}", 
                    aiResponse.Contains("SharePoint") ? "SharePoint keyword" : "No SharePoint keyword");
            }
            
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

        // Add MCP tools dynamically
        await AddMcpToolsToKernelAsync(kernel, accessToken);

        _logger.LogInformation("Added OneDrive, Calendar, Mail, ToDo plugins and MCP tools for user {UserId}.", userOid);

        // Log available functions for debugging
        var availableFunctions = kernel.Plugins.SelectMany(p => p.Select(f => $"{p.Name}.{f.Name}")).ToList();
        _logger.LogInformation("Available functions for user {UserId}: {Functions}", userOid, string.Join(", ", availableFunctions));

        return kernel;
    }

    private async Task AddMcpToolsToKernelAsync(Kernel kernel, string userAccessToken)
    {
        try
        {
            _logger.LogInformation("🔗 === MCP INTEGRATION START ===");
            _logger.LogInformation("🔗 Attempting to connect to MCP server for SharePoint tools...");
            
            // Get MCP server URL from configuration
            var mcpServerUrl = _configuration["McpServer:Url"] ?? "https://localhost:31339";
            _logger.LogInformation("🌐 MCP Server URL: {Url}", mcpServerUrl);

            // Try to implement proper MCP client connection using SSE transport
            IMcpClient mcpClient = null;
            try
            {
                _logger.LogInformation("🔧 Attempting to create MCP client using ModelContextProtocol v0.2.0-preview.2...");
                
                // Create the SSE endpoint URL
                var mcpServerEndpoint = new Uri($"{mcpServerUrl}/sse");
                _logger.LogInformation("📡 Attempting MCP connection to: {Endpoint}", mcpServerEndpoint);
                
                // Use the correct constructor pattern from the official examples
                var sseClientTransportOptions = new SseClientTransportOptions
                {
                    Endpoint = mcpServerEndpoint,
                    Name = "SharePointMCP"
                };
                
                var sseClientTransport = new SseClientTransport(sseClientTransportOptions);
                
                // Create the MCP client
                mcpClient = await McpClientFactory.CreateAsync(sseClientTransport);
                _logger.LogInformation("🎉 Successfully created MCP client!");
                
                // Test the connection by trying to list tools using the correct method name
                _logger.LogInformation("🔍 Testing MCP client connection by listing tools...");
                
                // Use the correct method name and proper async/await pattern
                var mcpTools = await mcpClient.ListToolsAsync();
                
                if (mcpTools == null)
                {
                    _logger.LogWarning("⚠️ ListToolsAsync returned null - MCP server may not be responding");
                    return;
                }
                
                var toolsCount = mcpTools.Count();
                _logger.LogInformation("🔧 Found {ToolCount} tools from MCP server", toolsCount);
                
                if (mcpTools.Any())
                {
                    _logger.LogInformation("📋 === MCP TOOLS DISCOVERED ===");
                    
                    // Convert MCP tools to Kernel functions using proper types
                    var kernelFunctions = new List<KernelFunction>();
                    var toolIndex = 0;
                    
                    foreach (var tool in mcpTools)
                    {
                        var toolName = tool.Name ?? "UnknownTool";
                        var toolDescription = tool.Description ?? "No description";
                        _logger.LogInformation("📝 Processing MCP tool: {ToolName} - {Description}", toolName, toolDescription);
                        
                        // Create a kernel function that wraps the MCP tool
                        var kernelFunction = CreateKernelFunctionFromMcpTool(tool, mcpClient, userAccessToken, toolIndex, kernel);
                        kernelFunctions.Add(kernelFunction);
                        
                        _logger.LogInformation("✅ Created kernel function for MCP tool: {ToolName}", toolName);
                        toolIndex++;
                    }
                    
                    // Add the functions as a plugin
                    kernel.Plugins.AddFromFunctions("SharePointMCP", kernelFunctions);
                    _logger.LogInformation("🎯 Added {FunctionCount} SharePoint MCP functions to kernel", kernelFunctions.Count);
                    
                    // Log the actual function names that were added
                    var functionNames = kernelFunctions.Select(f => f.Name).ToList();
                    _logger.LogInformation("📌 Available SharePoint functions: {Functions}", string.Join(", ", functionNames));
                }
                else
                {
                    _logger.LogWarning("⚠️ No tools found on MCP server - SharePoint functionality will be unavailable");
                }
            }
            catch (Exception transportEx)
            {
                _logger.LogError(transportEx, "❌ Failed to create MCP client connection");
                _logger.LogWarning("📋 This may be expected if the MCP server is not running");
                _logger.LogInformation("💡 To enable SharePoint: Start the MCP server on {Url}", mcpServerUrl);
            }
            
            _logger.LogInformation("✅ === MCP INTEGRATION COMPLETE ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to prepare MCP integration - SharePoint functionality will be unavailable");
            // Don't fail the entire request if MCP server preparation fails
        }
    }

    private KernelFunction CreateKernelFunctionFromMcpTool(object tool, IMcpClient mcpClient, string userAccessToken, int toolIndex, Kernel kernel)
    {
        // Extract tool properties using proper type access instead of reflection
        var toolName = tool.GetType().GetProperty("Name")?.GetValue(tool)?.ToString() ?? $"McpTool{toolIndex}";
        var toolDescription = tool.GetType().GetProperty("Description")?.GetValue(tool)?.ToString() ?? "MCP tool from SharePoint server";
        
        _logger.LogInformation("🔧 Creating kernel function for MCP tool: {ToolName} | Description: {Description}", toolName, toolDescription);
        
        // Create the actual kernel function that calls the MCP tool
        return KernelFunctionFactory.CreateFromMethod(
            async ([Description("Search query")] string query = "", 
             [Description("Number of results")] int count = 5, 
             [Description("List name")] string listName = "") =>
            {
                return await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("🚀 === MCP TOOL CALL START: {ToolName} ===", toolName);
                        _logger.LogInformation("📥 INPUT PARAMETERS:");
                        _logger.LogInformation("   🔍 Query: '{Query}'", query);
                        _logger.LogInformation("   📊 Count: {Count}", count);
                        _logger.LogInformation("   📁 List Name: '{ListName}'", listName);
                        _logger.LogInformation("   🔑 Access Token Length: {TokenLength} chars", userAccessToken?.Length ?? 0);
                        
                        // Prepare parameters for the MCP tool call with detailed logging
                        var parameters = new Dictionary<string, object>();
                        
                        // Add common parameters that SharePoint tools might expect
                        if (!string.IsNullOrEmpty(query))
                        {
                            parameters["query"] = query;
                            _logger.LogInformation("   ✅ Added query parameter: '{Query}'", query);
                        }
                        else
                        {
                            _logger.LogWarning("   ⚠️ Query parameter is empty - this might cause no results");
                        }
                        
                        if (count > 0)
                        {
                            parameters["count"] = count;
                            _logger.LogInformation("   ✅ Added count parameter: {Count}", count);
                        }
                        
                        if (!string.IsNullOrEmpty(listName))
                        {
                            parameters["listName"] = listName;
                            _logger.LogInformation("   ✅ Added listName parameter: '{ListName}'", listName);
                        }
                        
                        if (!string.IsNullOrEmpty(userAccessToken))
                        {
                            parameters["accessToken"] = userAccessToken;
                            _logger.LogInformation("   ✅ Added accessToken parameter (length: {Length})", userAccessToken.Length);
                        }
                        else
                        {
                            _logger.LogError("   ❌ CRITICAL: Access token is missing - SharePoint calls will likely fail!");
                        }
                        
                        // Try different parameter variations that the SharePoint MCP server might expect
                        parameters["searchQuery"] = query;  // Alternative parameter name
                        parameters["limit"] = count;        // Alternative parameter name
                        parameters["maxResults"] = count;   // Alternative parameter name
                        
                        // *** NEW: ADD RESPONSE MODE BASED ON ANALYSIS DETECTION ***
                        // Determine if this is an analysis request based on the query context
                        var isAnalysisRequest = IsAnalysisRequest(query);
                        var responseMode = isAnalysisRequest ? "analysis" : "card";
                        parameters["responseMode"] = responseMode;
                        
                        _logger.LogInformation("🎯 RESPONSE MODE DETECTION: Query='{Query}' -> Mode='{ResponseMode}' (Analysis={IsAnalysis})", 
                            query, responseMode, isAnalysisRequest);
                        
                        _logger.LogInformation("📤 FINAL PARAMETERS being sent to MCP server:");
                        foreach (var param in parameters)
                        {
                            if (param.Key == "accessToken")
                            {
                                _logger.LogInformation("   🔑 {Key}: [TOKEN-{Length}-CHARS]", param.Key, param.Value?.ToString()?.Length ?? 0);
                            }
                            else
                            {
                                _logger.LogInformation("   📋 {Key}: '{Value}'", param.Key, param.Value);
                            }
                        }
                        
                        _logger.LogInformation("🌐 Calling MCP server tool: {ToolName}", toolName);
                        
                        // Call the MCP tool using the proper interface
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var result = await mcpClient.CallToolAsync(toolName, parameters);
                        stopwatch.Stop();
                        
                        _logger.LogInformation("⏱️ MCP call completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                        
                        // Detailed response analysis
                        _logger.LogInformation("📥 === MCP RESPONSE ANALYSIS ===");
                        
                        var resultString = result.ToString();
                        
                        // Enhanced MCP response data extraction
                        if (result != null && result.GetType().Name == "CallToolResponse")
                        {
                            try
                            {
                                _logger.LogInformation("🔍 Attempting to extract data from CallToolResponse object...");
                                
                                // First try to get Content property (most common)
                                var contentProperty = result.GetType().GetProperty("Content");
                                if (contentProperty != null)
                                {
                                    var contentValue = contentProperty.GetValue(result);
                                    if (contentValue != null)
                                    {
                                        _logger.LogInformation("📋 Content property type: {Type}", contentValue.GetType().Name);
                                        
                                        // Check if Content is a collection/list of Content objects
                                        if (contentValue is System.Collections.IEnumerable contentCollection && 
                                            !(contentValue is string))
                                        {
                                            _logger.LogInformation("🔍 Content is a collection, extracting individual items...");
                                            
                                            var contentItems = new List<string>();
                                            foreach (var item in contentCollection)
                                            {
                                                if (item != null)
                                                {
                                                    // Try to get Text property from Content object
                                                    var textProperty = item.GetType().GetProperty("Text");
                                                    if (textProperty != null)
                                                    {
                                                        var textValue = textProperty.GetValue(item);
                                                        if (textValue != null)
                                                        {
                                                            contentItems.Add(textValue.ToString());
                                                            _logger.LogInformation("✅ Extracted text from Content item: {Length} chars", textValue.ToString().Length);
                                                        }
                                                    }
                                                    // If no Text property, try serializing the entire item
                                                    else
                                                    {
                                                        try
                                                        {
                                                            var serializedItem = JsonSerializer.Serialize(item);
                                                            contentItems.Add(serializedItem);
                                                            _logger.LogInformation("✅ Serialized Content item: {Length} chars", serializedItem.Length);
                                                        }
                                                        catch
                                                        {
                                                            contentItems.Add(item.ToString());
                                                            _logger.LogInformation("⚠️ Used ToString() for Content item");
                                                        }
                                                    }
                                                }
                                            }
                                            
                                            if (contentItems.Any())
                                            {
                                                // Join all content items (usually just one)
                                                resultString = string.Join("\n", contentItems);
                                                _logger.LogInformation("✅ Extracted {Count} content items from CallToolResponse.Content", contentItems.Count);
                                            }
                                        }
                                        else
                                        {
                                            // Content is not a collection, use it directly
                                            resultString = contentValue.ToString();
                                            _logger.LogInformation("✅ Extracted data directly from CallToolResponse.Content");
                                        }
                                    }
                                }
                                
                                // If no Content property, look for other common response properties
                                if (string.IsNullOrEmpty(resultString))
                                {
                                    var resultProperty = result.GetType().GetProperty("Result");
                                    if (resultProperty != null)
                                    {
                                        var resultValue = resultProperty.GetValue(result);
                                        resultString = resultValue?.ToString();
                                        _logger.LogInformation("✅ Extracted data from CallToolResponse.Result");
                                    }
                                }
                                
                                // If still no data, look for Data property
                                if (string.IsNullOrEmpty(resultString))
                                {
                                    var dataProperty = result.GetType().GetProperty("Data");
                                    if (dataProperty != null)
                                    {
                                        var dataValue = dataProperty.GetValue(result);
                                        if (dataValue != null)
                                        {
                                            resultString = JsonSerializer.Serialize(dataValue);
                                            _logger.LogInformation("✅ Serialized CallToolResponse.Data");
                                        }
                                    }
                                }
                                
                                // If still no data, try serializing the entire response object
                                if (string.IsNullOrEmpty(resultString))
                                {
                                    resultString = JsonSerializer.Serialize(result);
                                    _logger.LogInformation("✅ Serialized entire CallToolResponse object");
                                }
                            }
                            catch (Exception extractEx)
                            {
                                _logger.LogWarning(extractEx, "⚠️ Failed to extract data from CallToolResponse, falling back to ToString()");
                                resultString = result.ToString();
                            }
                        }
                        
                        var resultLength = resultString?.Length ?? 0;
                        
                        _logger.LogInformation("📄 Response length: {Length} characters", resultLength);
                        _logger.LogInformation("🔍 === EXTRACTED RESPONSE DATA ===");
                        
                        if (resultLength == 0)
                        {
                            _logger.LogWarning("⚠️ MCP tool returned empty response after extraction");
                            return $"🔍 No SharePoint sites found matching your query '{query}'. Try a different search term or check your SharePoint access.";
                        }
                        
                        if (resultLength < 50)
                        {
                            _logger.LogInformation("📄 Short response: '{Response}'", resultString);
                        }
                        else if (resultLength < 500)
                        {
                            _logger.LogInformation("📄 Full response: {Response}", resultString);
                        }
                        else
                        {
                            _logger.LogInformation("📄 Response preview (first 500 chars): '{Preview}...'", 
                                resultString.Length > 500 ? resultString.Substring(0, 500) : resultString);
                            _logger.LogInformation("📄 Response preview (last 200 chars): '...{End}'", 
                                resultString.Length > 200 ? resultString.Substring(resultString.Length - 200) : resultString);
                        }
                        
                        // Check for common error patterns
                        if (resultString.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            resultString.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                            resultString.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogError("❌ ERROR DETECTED in MCP response: {Response}", resultString);
                            return $"❌ SharePoint search error: {resultString}";
                        }
                        
                        // === NEW: SharePoint-specific response formatting ===
                        // Check if this is a SharePoint tool response and apply appropriate formatting
                        if (IsSharePointTool(toolName))
                        {
                            _logger.LogInformation("🔍 Detected SharePoint tool response, checking for structured data...");
                            
                            // Try to parse the JSON response from SharePoint MCP tool
                            try
                            {
                                var jsonDoc = JsonDocument.Parse(resultString);
                                if (jsonDoc.RootElement.TryGetProperty("success", out var successElement) && 
                                    successElement.GetBoolean() &&
                                    jsonDoc.RootElement.TryGetProperty("sites", out var sitesElement))
                                {
                                    _logger.LogInformation("🎯 Found SharePoint sites data in MCP response, setting kernel data directly...");
                                    
                                    var sites = JsonSerializer.Deserialize<object[]>(sitesElement.GetRawText());
                                    var message = jsonDoc.RootElement.TryGetProperty("message", out var messageElement) 
                                        ? messageElement.GetString() : $"Found {sites.Length} SharePoint sites.";
                                    var totalCount = jsonDoc.RootElement.TryGetProperty("totalCount", out var countElement) 
                                        ? countElement.GetInt32() : sites.Length;
                                    
                                    _logger.LogInformation("🎯 Prepared SharePoint structured data: {Count} sites, Message: '{Message}'", sites.Length, message);
                                    
                                    // *** DIRECTLY SET KERNEL DATA FOR SHAREPOINT CARDS ***
                                    // This will be detected in the main response processing
                                    kernel.Data["HasStructuredData"] = "true";
                                    kernel.Data["StructuredDataType"] = "sharepoint";
                                    kernel.Data["StructuredDataCount"] = sites.Length;
                                    kernel.Data["SharePointCards"] = sites;
                                    kernel.Data["SharePointFunctionResponse"] = message;
                                    
                                    _logger.LogInformation("✅ Successfully set SharePoint card data in kernel: {Count} sites", sites.Length);
                                    
                                    // Return a simple response that the AI can use
                                    return message ?? $"Found {sites.Length} SharePoint sites.";
                                }
                                else
                                {
                                    _logger.LogInformation("⚠️ SharePoint MCP response doesn't contain expected structure");
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Failed to parse SharePoint MCP response as JSON");
                            }
                        }
                        
                        _logger.LogInformation("✅ === MCP TOOL CALL SUCCESS: {ToolName} ===", toolName);
                        
                        return resultString ?? $"✅ SharePoint search completed successfully using {toolName}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ === MCP TOOL CALL FAILED: {ToolName} ===", toolName);
                        _logger.LogError("   Exception Type: {ExceptionType}", ex.GetType().Name);
                        _logger.LogError("   Exception Message: {Message}", ex.Message);
                        
                        if (ex.InnerException != null)
                        {
                            _logger.LogError("   Inner Exception: {InnerMessage}", ex.InnerException.Message);
                        }
                        
                        // Provide helpful error messages based on exception type
                        if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"⏱️ SharePoint search timed out. The server may be slow or unavailable. Please try again.";
                        }
                        else if (ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"🔒 Access denied to SharePoint. Please check your permissions and try signing in again.";
                        }
                        else if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"🔍 SharePoint service not found. Please check if the SharePoint MCP server is running.";
                        }
                        
                        return $"❌ SharePoint search failed: {ex.Message}. Please try again or contact support.";
                    }
                });
            },
            functionName: toolName,
            description: toolDescription
        );
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

CRITICAL RULE: NEVER MAKE UP DATA - ONLY USE ACTUAL FUNCTION RESULTS!

SHAREPOINT DATA RULES:
- ALWAYS call SharePoint functions when users ask for SharePoint sites
- NEVER generate fake site names like 'Marketing Team Collaboration' or 'Project Management Hub' 
- ONLY use the EXACT site titles, URLs, and descriptions returned by the SharePoint functions
- If SharePoint functions return real data like 'nirtest20250527b' or 'test20250527a', use those EXACT names
- If functions return empty results, say 'No SharePoint sites found' - do NOT make up examples

MANDATORY FUNCTION CALLING RULES:

1. SHAREPOINT SITES - When user asks for SharePoint sites, you MUST call one of these functions:
   - For general SharePoint requests: ""SharePoint sites"", ""my sites"", ""show sites"" → CALL ANY available SharePoint function
   - For recent sites: ""recent SharePoint"", ""last N sites"", ""latest sites"" → CALL SearchRecentSharePointSites if available
   - For search queries: ""find SharePoint"", ""search for [term]"" → CALL SearchSharePointSites or FindSharePointSitesByKeyword
   - For advanced searches: complex queries → CALL SearchSharePointSitesAdvanced if available
   
   CRITICAL SharePoint Examples:
   - User: ""show me my sharepoint sites"" → MUST call available SharePoint MCP function
   - User: ""find sharepoint sites about project"" → MUST call SharePoint search function with query=""project""
   - User: ""my recent sharepoint sites"" → MUST call SharePoint function (any available)
   
   ALWAYS try SharePoint functions when users mention: sharepoint, sites, collaboration, documents, teams

   CRITICAL: WHEN SHAREPOINT FUNCTIONS RETURN DATA:
   - You MUST use the EXACT site names, URLs, and data returned by the function
   - NEVER substitute with generic examples or made-up data
   - If the function returns 'nirtest20250527b - MyPrivateChannel', use that EXACT title
   - If the function returns 'https://rnico.sharepoint.com/sites/test20250527a', use that EXACT URL
   - Present the data as returned by the function, do not modify or improve it

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

DEBUGGING NOTES:
- SharePoint MCP functions should be available and will be called automatically
- If SharePoint search returns no results, that's normal feedback - don't assume the function failed
- Always attempt function calls first, even if you're unsure about the data
- When functions return data, USE THAT EXACT DATA - never substitute with examples

ABSOLUTELY FORBIDDEN:
- Making up SharePoint site names like 'Marketing Team Collaboration', 'Project Management Hub', 'HR Resources'
- Creating fake URLs like 'https://yourorganization.sharepoint.com/sites/marketing'
- Generating example data when real function results are available
- Ignoring function results and providing generic responses

NEVER say ""I cannot access"" or ""I don't have the ability"" - these functions ARE available.
ALWAYS attempt to call the appropriate function first.
If a function call fails, THEN explain the error from the function result.
NEVER make up data when real function results are available.

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

    private bool IsSharePointTool(string toolName)
    {
        // Implement the logic to determine if a tool is a SharePoint tool
        // This is a placeholder and should be replaced with the actual implementation
        return toolName.Contains("SharePoint", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAnalysisRequest(string query)
    {
        if (string.IsNullOrEmpty(query)) return false;
        
        var queryLower = query.ToLower();
        
        // Check for analysis keywords similar to ToDoPlugin pattern
        var analysisKeywords = new[]
        {
            "summarize", "summary", "analyze", "analysis", "what about", "content overview",
            "which site", "what should i", "recommend", "priority", "prioritize", "tackle first",
            "focus on", "start with", "most important", "urgent", "advice", "suggest",
            "help me decide", "what to do", "compare", "evaluate", "assess", "review",
            "insights", "patterns", "trends", "overview", "breakdown", "deep dive"
        };
        
        return analysisKeywords.Any(keyword => queryLower.Contains(keyword));
    }

    private async Task<string> ProcessSharePointResponse(Kernel kernel, string resultString, string toolName, string query, bool isAnalysisMode)
    {
        // Implement the logic to process a SharePoint response based on the analysis mode
        // This is a placeholder and should be replaced with the actual implementation
        if (isAnalysisMode)
        {
            // Analysis mode logic
            return $"Analysis mode response for {toolName} with query '{query}'";
        }
        else
        {
            // Listing mode logic
            return $"Listing mode response for {toolName} with query '{query}'";
        }
    }
}