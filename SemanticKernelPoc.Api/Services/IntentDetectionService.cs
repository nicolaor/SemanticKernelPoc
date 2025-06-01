using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelPoc.Api.Models;
using System.Text.Json;

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace SemanticKernelPoc.Api.Services;

public interface IIntentDetectionService
{
    Task<UserIntent> ClassifyIntentAsync(string userMessage, string userId);
    Task<StructuredAIResponse> GenerateStructuredResponseAsync(string userMessage, string userId, string userName, UserIntent intent);
}

public class IntentDetectionService : IIntentDetectionService
{
    private readonly Kernel _kernel;
    private readonly ILogger<IntentDetectionService> _logger;

    public IntentDetectionService(Kernel kernel, ILogger<IntentDetectionService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<UserIntent> ClassifyIntentAsync(string userMessage, string userId)
    {
        try
        {
            var prompt = $@"Analyze the user's message and classify their intent. Return a JSON object with the following structure:
{{
    ""intent"": ""list|search|create|update|delete|analyze|help"",
    ""dataType"": ""task|email|calendar|file|sharepoint|general"",
    ""confidence"": 0.0-1.0,
    ""parameters"": {{""key"": ""value""}},
    ""wantsCards"": true|false
}}

Intent definitions:
- list: User wants to see/browse items (my tasks, my emails, upcoming events)
- search: User wants to find specific items (find emails from John, search tasks about project)
- create: User wants to create something new (create task, schedule meeting, send email)
- update: User wants to modify existing items (mark task complete, update appointment)
- delete: User wants to remove items (delete task, cancel meeting)
- analyze: User wants analysis/summary (summarize my tasks, analyze my schedule)
- help: User needs assistance or information

Data type definitions:
- task: ToDo items, tasks, reminders, notes
- email: Emails, messages, communication
- calendar: Appointments, meetings, events, schedule
- file: OneDrive files, documents, folders
- sharepoint: SharePoint sites, content, collaboration
- general: General questions, help, capabilities

WantsCards should be true for list/search intents, false for analyze/help intents.

User message: ""{userMessage}""

Respond with only the JSON object, no additional text.";

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                MaxTokens = 300,
                Temperature = 0.1,
                ResponseFormat = "json_object"
            };

            var response = await _kernel.InvokePromptAsync(prompt, new KernelArguments(executionSettings));
            var jsonResponse = response.ToString();

            var intent = JsonSerializer.Deserialize<UserIntent>(jsonResponse);
            return intent ?? new UserIntent { Intent = "help", DataType = "general", Confidence = 0.5 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying user intent for message: {Message}", userMessage);
            
            // Fallback intent detection using simple keyword matching
            return ClassifyIntentFallback(userMessage);
        }
    }

    public async Task<StructuredAIResponse> GenerateStructuredResponseAsync(string userMessage, string userId, string userName, UserIntent intent)
    {
        try
        {
            var responseType = DetermineResponseType(intent);
            var prompt = CreateStructuredPrompt(userMessage, userName, intent, responseType);

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                MaxTokens = 1000,
                Temperature = 0.1,
                ResponseFormat = "json_object"
            };

            var response = await _kernel.InvokePromptAsync(prompt, new KernelArguments(executionSettings));
            var jsonResponse = response.ToString();

            return DeserializeStructuredResponse(jsonResponse, responseType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating structured response for intent: {Intent}", intent.Intent);
            
            return new ErrorResponse
            {
                Error = "I encountered an error processing your request. Please try rephrasing your question.",
                Summary = "Processing error",
                Suggestions = new List<string> { "Try asking a more specific question", "Check if the service is available" },
                IsRecoverable = true
            };
        }
    }

    private UserIntent ClassifyIntentFallback(string userMessage)
    {
        var message = userMessage.ToLowerInvariant();
        
        // Intent classification
        string intent = "help";
        if (message.Contains("list") || message.Contains("show") || message.Contains("get") || message.Contains("my "))
            intent = "list";
        else if (message.Contains("search") || message.Contains("find"))
            intent = "search";
        else if (message.Contains("create") || message.Contains("add") || message.Contains("new"))
            intent = "create";
        else if (message.Contains("update") || message.Contains("edit") || message.Contains("change") || message.Contains("complete"))
            intent = "update";
        else if (message.Contains("analyze") || message.Contains("summary") || message.Contains("summarize"))
            intent = "analyze";

        // Data type classification
        string dataType = "general";
        if (message.Contains("task") || message.Contains("todo") || message.Contains("reminder"))
            dataType = "task";
        else if (message.Contains("email") || message.Contains("mail") || message.Contains("message"))
            dataType = "email";
        else if (message.Contains("calendar") || message.Contains("meeting") || message.Contains("appointment") || message.Contains("event"))
            dataType = "calendar";
        else if (message.Contains("file") || message.Contains("document") || message.Contains("onedrive"))
            dataType = "file";
        else if (message.Contains("sharepoint") || message.Contains("site"))
            dataType = "sharepoint";

        return new UserIntent
        {
            Intent = intent,
            DataType = dataType,
            Confidence = 0.7,
            WantsCards = intent == "list" || intent == "search",
            Parameters = new Dictionary<string, string>()
        };
    }

    private string DetermineResponseType(UserIntent intent)
    {
        return intent.DataType switch
        {
            "task" => nameof(StructuredTaskResponse),
            "email" => nameof(StructuredEmailResponse),
            "calendar" => nameof(StructuredCalendarResponse),
            "file" or "sharepoint" => nameof(InfoResponse),
            _ => intent.Intent == "analyze" ? nameof(AnalysisResponse) : nameof(InfoResponse)
        };
    }

    private string CreateStructuredPrompt(string userMessage, string userName, UserIntent intent, string responseType)
    {
        var baseFields = @"
            {
                ""type"": ""string - the response type"",
                ""summary"": ""string - brief summary of the response"",
                ""hasCards"": ""boolean - whether response contains card data"",
                ""confidence"": ""number - confidence level 0.0-1.0""
            }";

        var specificFields = responseType switch
        {
            nameof(StructuredTaskResponse) => @"
                ""tasks"": ""array - list of task objects if hasCards is true"",
                ""taskCount"": ""number - count of tasks"",
                ""message"": ""string - human readable message about tasks"",
                ""action"": ""string - action performed (list, create, update, search)""
                ",
            nameof(StructuredEmailResponse) => @"
                ""emails"": ""array - list of email objects if hasCards is true"", 
                ""emailCount"": ""number - count of emails"",
                ""message"": ""string - human readable message about emails"",
                ""action"": ""string - action performed (list, search, create, send)""
                ",
            nameof(StructuredCalendarResponse) => @"
                ""events"": ""array - list of event objects if hasCards is true"",
                ""eventCount"": ""number - count of events"", 
                ""message"": ""string - human readable message about events"",
                ""action"": ""string - action performed (list, create, update, search)""
                ",
            nameof(AnalysisResponse) => @"
                ""analysis"": ""string - the analysis or summary text"",
                ""insights"": ""array - key insights from analysis"",
                ""statistics"": ""object - numerical stats if applicable"",
                ""recommendations"": ""array - recommendations based on analysis""
                ",
            _ => @"
                ""content"": ""string - main information content"",
                ""details"": ""object - additional details if needed"",
                ""isSuccess"": ""boolean - whether this is success or error""
                "
        };

        return $@"
             You are responding to: ""{userMessage}""
             User: {userName}
             Detected intent: {intent.Intent}
             Data type: {intent.DataType}
             Wants cards: {intent.WantsCards}

             Generate a structured JSON response with these fields:
             {{
                 {baseFields.Trim('{', '}')},
                 {specificFields}
             }}

             Guidelines:
             - Set hasCards to true only if the user wants to see specific items (list/search intents)
             - For hasCards=true, include empty arrays for data fields (actual data comes from functions)
             - For analysis intents, set hasCards=false and focus on text content
             - Keep messages concise and helpful
             - Set appropriate confidence levels

             Respond with only the JSON object, no additional text.";
    }

    private StructuredAIResponse DeserializeStructuredResponse(string jsonResponse, string responseType)
    {
        try
        {
            return responseType switch
            {
                nameof(StructuredTaskResponse) => JsonSerializer.Deserialize<StructuredTaskResponse>(jsonResponse) ?? new StructuredTaskResponse(),
                nameof(StructuredEmailResponse) => JsonSerializer.Deserialize<StructuredEmailResponse>(jsonResponse) ?? new StructuredEmailResponse(),
                nameof(StructuredCalendarResponse) => JsonSerializer.Deserialize<StructuredCalendarResponse>(jsonResponse) ?? new StructuredCalendarResponse(),
                nameof(AnalysisResponse) => JsonSerializer.Deserialize<AnalysisResponse>(jsonResponse) ?? new AnalysisResponse(),
                _ => JsonSerializer.Deserialize<InfoResponse>(jsonResponse) ?? new InfoResponse()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing structured response: {Json}", jsonResponse);
            
            return new ErrorResponse
            {
                Error = "Failed to generate structured response",
                Summary = "Response format error",
                IsRecoverable = true
            };
        }
    }
} 