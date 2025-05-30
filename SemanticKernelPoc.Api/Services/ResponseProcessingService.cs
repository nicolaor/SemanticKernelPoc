using System.Text.Json;
using System.Text.RegularExpressions;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Plugins.Calendar;

namespace SemanticKernelPoc.Api.Services;

public interface IResponseProcessingService
{
    ChatResponse ProcessResponse(string content, string sessionId, string userId, string userName);
}

public class ResponseProcessingService : IResponseProcessingService
{
    private readonly ILogger<ResponseProcessingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ResponseProcessingService(ILogger<ResponseProcessingService> logger)
    {
        _logger = logger;
        
        // Configure JSON options to match the API serialization settings
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public ChatResponse ProcessResponse(string content, string sessionId, string userId, string userName)
    {
        _logger.LogInformation("Processing response content: {Content}", content);
        
        var response = new ChatResponse
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            UserId = userId,
            UserName = userName,
            IsAiResponse = true,
            Timestamp = DateTime.UtcNow,
            Content = content
        };

        // Try to extract card data
        var cardData = ExtractCardData(content);
        if (cardData != null)
        {
            _logger.LogInformation("Extracted card data: Type={Type}, Count={Count}", cardData.Type, cardData.Count);
            response.Cards = cardData;
            response.Metadata = new ResponseMetadata
            {
                HasCards = true
            };
            
            // Clean the content - remove the card data part
            response.Content = CleanContentFromCardData(content);
            _logger.LogInformation("Cleaned content: {CleanedContent}", response.Content);
        }
        else
        {
            _logger.LogInformation("No card data found in content");
        }

        return response;
    }

    private CardData? ExtractCardData(string content)
    {
        try
        {
            _logger.LogInformation("Checking content for card data patterns...");
            
            // Skip analysis data - these should be processed as text by the AI
            if (content.Contains("TASK_ANALYSIS:") || content.Contains("EMAIL_ANALYSIS:") || content.Contains("CALENDAR_ANALYSIS:"))
            {
                _logger.LogInformation("Found analysis data - skipping card extraction to let AI process as text");
                return null;
            }
            
            // Check for task cards
            if (content.Contains("TASK_CARDS:"))
            {
                _logger.LogInformation("Found TASK_CARDS pattern in content");
                var taskData = ExtractJsonData(content, "TASK_CARDS:");
                if (taskData != null)
                {
                    _logger.LogInformation("Extracted task JSON data: {TaskData}", taskData);
                    var tasks = JsonSerializer.Deserialize<List<TaskCardData>>(taskData, _jsonOptions);
                    if (tasks != null)
                    {
                        _logger.LogInformation("Successfully deserialized {Count} tasks", tasks.Count);
                        return new CardData
                        {
                            Type = "tasks",
                            Data = tasks,
                            Count = tasks.Count
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize task data");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to extract task JSON data");
                }
            }

            // Check for email cards
            if (content.Contains("EMAIL_CARDS:"))
            {
                _logger.LogInformation("Found EMAIL_CARDS pattern in content");
                var emailData = ExtractJsonData(content, "EMAIL_CARDS:");
                if (emailData != null)
                {
                    _logger.LogInformation("Extracted email JSON data: {EmailData}", emailData);
                    var emails = JsonSerializer.Deserialize<List<EmailCardData>>(emailData, _jsonOptions);
                    if (emails != null)
                    {
                        _logger.LogInformation("Successfully deserialized {Count} emails", emails.Count);
                        return new CardData
                        {
                            Type = "emails",
                            Data = emails,
                            Count = emails.Count
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize email data");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to extract email JSON data");
                }
            }

            // Check for calendar cards
            if (content.Contains("CALENDAR_CARDS:"))
            {
                _logger.LogInformation("Found CALENDAR_CARDS pattern in content");
                var calendarData = ExtractJsonData(content, "CALENDAR_CARDS:");
                if (calendarData != null)
                {
                    _logger.LogInformation("Extracted calendar JSON data: {CalendarData}", calendarData);
                    var calendar = JsonSerializer.Deserialize<CalendarCardsData>(calendarData, _jsonOptions);
                    if (calendar != null)
                    {
                        _logger.LogInformation("Successfully deserialized calendar data with {Count} events", calendar.Count);
                        return new CardData
                        {
                            Type = "calendar",
                            Data = calendar.Events,
                            Count = calendar.Count,
                            UserName = calendar.UserName,
                            TimeRange = calendar.TimeRange
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize calendar data");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to extract calendar JSON data");
                }
            }

            // Check for capabilities
            if (content.Contains("I can assist you with a variety of tasks") || 
                content.Contains("Calendar Management") || 
                content.Contains("Note-Taking") || 
                content.Contains("Email Management"))
            {
                return new CardData
                {
                    Type = "capabilities",
                    Data = new { capabilities = content },
                    Count = 1
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract card data from content");
        }

        return null;
    }

    private string? ExtractJsonData(string content, string prefix)
    {
        try
        {
            var startIndex = content.IndexOf(prefix);
            if (startIndex == -1) return null;

            var jsonStart = startIndex + prefix.Length;
            var jsonContent = content[jsonStart..].Trim();

            // Try to find the end of the JSON by looking for balanced braces
            var braceCount = 0;
            var inString = false;
            var escaped = false;
            var endIndex = -1;

            for (int i = 0; i < jsonContent.Length; i++)
            {
                var ch = jsonContent[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (ch == '[' || ch == '{')
                    {
                        braceCount++;
                    }
                    else if (ch == ']' || ch == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            endIndex = i + 1;
                            break;
                        }
                    }
                }
            }

            if (endIndex > 0)
            {
                return jsonContent[..endIndex];
            }

            // Fallback: try to parse until end of line or content
            var lines = jsonContent.Split('\n');
            return lines[0].Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract JSON data with prefix {Prefix}", prefix);
            return null;
        }
    }

    private string CleanContentFromCardData(string content)
    {
        // Remove card data lines but keep any explanatory text
        var patterns = new[]
        {
            @"TASK_CARDS:.*",
            @"EMAIL_CARDS:.*",
            @"CALENDAR_CARDS:.*"
        };

        var cleanedContent = content;
        foreach (var pattern in patterns)
        {
            cleanedContent = Regex.Replace(cleanedContent, pattern, "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        // Clean up extra whitespace
        cleanedContent = Regex.Replace(cleanedContent, @"\n\s*\n", "\n", RegexOptions.Multiline);
        cleanedContent = cleanedContent.Trim();

        // If content is empty after cleaning, provide a default message
        if (string.IsNullOrWhiteSpace(cleanedContent))
        {
            cleanedContent = "Here are your results:";
        }

        return cleanedContent;
    }
} 