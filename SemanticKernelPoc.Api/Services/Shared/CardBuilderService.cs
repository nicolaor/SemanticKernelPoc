using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SemanticKernelPoc.Api.Services.Shared;

/// <summary>
/// Service to build standardized cards for different data types and eliminate code duplication
/// </summary>
public interface ICardBuilderService
{
    /// <summary>
    /// Build email cards with standardized structure
    /// </summary>
    List<object> BuildEmailCards<T>(IEnumerable<T> messages, Func<T, int, object> mapper);
    
    /// <summary>
    /// Build task cards with standardized structure
    /// </summary>
    List<object> BuildTaskCards<T>(IEnumerable<T> tasks, Func<T, int, object> mapper);
    
    /// <summary>
    /// Build calendar event cards with standardized structure
    /// </summary>
    List<object> BuildCalendarCards<T>(IEnumerable<T> events, Func<T, int, object> mapper);
    
    /// <summary>
    /// Set card data in kernel with standardized approach
    /// </summary>
    void SetCardData(Kernel kernel, string dataType, List<object> cards, int count, string functionResponse);
}

/// <summary>
/// Implementation of card builder service
/// </summary>
public class CardBuilderService : ICardBuilderService
{
    private readonly ILogger<CardBuilderService> _logger;

    public CardBuilderService(ILogger<CardBuilderService> logger)
    {
        _logger = logger;
    }

    public List<object> BuildEmailCards<T>(IEnumerable<T> messages, Func<T, int, object> mapper)
    {
        var cards = new List<object>();
        var index = 0;
        
        foreach (var message in messages)
        {
            try
            {
                var card = mapper(message, index);
                cards.Add(card);
                index++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building email card at index {Index}", index);
            }
        }
        
        return cards;
    }

    public List<object> BuildTaskCards<T>(IEnumerable<T> tasks, Func<T, int, object> mapper)
    {
        var cards = new List<object>();
        var index = 0;
        
        _logger.LogInformation("🔍 BuildTaskCards: Starting with {TaskCount} tasks", tasks?.Count() ?? 0);
        
        foreach (var task in tasks)
        {
            try
            {
                _logger.LogInformation("🔍 BuildTaskCards: Processing task at index {Index}", index);
                var card = mapper(task, index);
                _logger.LogInformation("🔍 BuildTaskCards: Card created: {Card}", JsonSerializer.Serialize(card));
                cards.Add(card);
                index++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error building task card at index {Index}", index);
            }
        }
        
        _logger.LogInformation("✅ BuildTaskCards: Completed with {CardCount} cards", cards.Count);
        return cards;
    }

    public List<object> BuildCalendarCards<T>(IEnumerable<T> events, Func<T, int, object> mapper)
    {
        var cards = new List<object>();
        var index = 0;
        
        foreach (var eventItem in events)
        {
            try
            {
                var card = mapper(eventItem, index);
                cards.Add(card);
                index++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building calendar card at index {Index}", index);
            }
        }
        
        return cards;
    }

    public void SetCardData(Kernel kernel, string dataType, List<object> cards, int count, string functionResponse)
    {
        try
        {
            // Set the card data using the expected keys that ChatController looks for
            kernel.Data[$"{CapitalizeFirst(dataType)}Cards"] = cards;
            kernel.Data["HasStructuredData"] = "true";
            kernel.Data["StructuredDataType"] = dataType.ToLower();
            kernel.Data["StructuredDataCount"] = count;
            
            if (!string.IsNullOrEmpty(functionResponse))
            {
                kernel.Data[$"{CapitalizeFirst(dataType)}FunctionResponse"] = functionResponse;
            }
            
            _logger.LogInformation("✅ Set {DataType} cards data: {Count} items", dataType, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error setting {DataType} cards data", dataType);
        }
    }
    
    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input[1..].ToLower();
    }
} 