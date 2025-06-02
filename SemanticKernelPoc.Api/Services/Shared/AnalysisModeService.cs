using Microsoft.SemanticKernel;

namespace SemanticKernelPoc.Api.Services.Shared;

/// <summary>
/// Service to handle analysis mode logic and AI summarization across plugins
/// </summary>
public interface IAnalysisModeService
{
    /// <summary>
    /// Generate AI summary for a collection of items
    /// </summary>
    Task<string> GenerateAISummaryAsync<T>(
        Kernel kernel, 
        IEnumerable<T> items, 
        string itemType, 
        string userName, 
        Func<T, object> itemMapper,
        string? customPrompt = null);
    
    /// <summary>
    /// Create fallback summary when AI is not available
    /// </summary>
    string CreateFallbackSummary<T>(
        IEnumerable<T> items, 
        string itemType, 
        string userName,
        Func<T, bool> completedSelector = null,
        Func<T, bool> highPrioritySelector = null);
}

public class AnalysisModeService : IAnalysisModeService
{
    private readonly ILogger<AnalysisModeService> _logger;

    public AnalysisModeService(ILogger<AnalysisModeService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateAISummaryAsync<T>(
        Kernel kernel, 
        IEnumerable<T> items, 
        string itemType, 
        string userName, 
        Func<T, object> itemMapper,
        string? customPrompt = null)
    {
        _logger.LogInformation("🔍 GenerateAISummaryAsync: Starting analysis for {ItemType} with {Count} items", itemType, items?.Count() ?? 0);
        
        var itemList = items.ToList();
        var mappedItems = itemList.Select(itemMapper).ToList();
        
        _logger.LogInformation("🔍 GenerateAISummaryAsync: Mapped {Count} items for analysis", mappedItems.Count);

        // Create default prompt if none provided
        var prompt = customPrompt ?? CreateDefaultPrompt(mappedItems, itemType, itemList.Count);
        _logger.LogInformation("🔍 GenerateAISummaryAsync: Created prompt: {Prompt}", prompt.Substring(0, Math.Min(200, prompt.Length)) + "...");

        // Try to use the passed kernel directly instead of looking for a global one
        if (kernel != null)
        {
            try
            {
                _logger.LogInformation("🔍 GenerateAISummaryAsync: Attempting to generate AI summary using provided kernel");
                var summaryResult = await kernel.InvokePromptAsync(prompt);
                var summary = summaryResult.ToString();
                _logger.LogInformation("✅ GenerateAISummaryAsync: Successfully generated AI summary: {Summary}", summary.Substring(0, Math.Min(100, summary.Length)) + "...");
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate AI summary for {ItemType}", itemType);
                // Return fallback summary
                var fallback = CreateFallbackSummary(itemList, itemType, userName);
                _logger.LogInformation("🔄 GenerateAISummaryAsync: Using fallback summary: {Fallback}", fallback);
                return fallback;
            }
        }

        // If no kernel available, return fallback
        _logger.LogWarning("⚠️ GenerateAISummaryAsync: No kernel available, using fallback summary");
        return CreateFallbackSummary(itemList, itemType, userName);
    }

    public string CreateFallbackSummary<T>(
        IEnumerable<T> items, 
        string itemType, 
        string userName,
        Func<T, bool> completedSelector = null,
        Func<T, bool> highPrioritySelector = null)
    {
        var itemList = items.ToList();
        var count = itemList.Count;
        
        var summary = $"Found {count} {itemType} for {userName}";

        if (completedSelector != null)
        {
            var completedCount = itemList.Count(completedSelector);
            summary += $". {completedCount} completed";
        }

        if (highPrioritySelector != null)
        {
            var highPriorityCount = itemList.Count(highPrioritySelector);
            summary += $", {highPriorityCount} high priority";
        }

        return summary + $". Check your {itemType} for full details.";
    }

    private string CreateDefaultPrompt(IEnumerable<object> mappedItems, string itemType, int count)
    {
        return $@"Please provide a concise summary of these {count} recent {itemType} for the user. Focus on the main topics, key information, and actionable items. Don't just list metadata - summarize what the {itemType} are actually about:

{string.Join("\n\n", mappedItems.Select((item, i) => $"{itemType} {i + 1}:\n{FormatItemForPrompt(item)}"))}

Provide a helpful summary that tells the user what these {itemType} are about, not just who sent them or basic metadata.";
    }

    private string FormatItemForPrompt(object item)
    {
        if (item == null) return "No data available";
        
        // Use reflection to format the object properties for the prompt
        var properties = item.GetType().GetProperties();
        var formatted = new List<string>();

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(item);
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    var propertyName = prop.Name;
                    // Convert camelCase to friendly names
                    if (propertyName.Length > 1)
                    {
                        propertyName = char.ToUpper(propertyName[0]) + propertyName[1..];
                    }
                    formatted.Add($"{propertyName}: {value}");
                }
            }
            catch
            {
                // Skip properties that can't be accessed
                continue;
            }
        }

        return string.Join("\n", formatted);
    }
} 