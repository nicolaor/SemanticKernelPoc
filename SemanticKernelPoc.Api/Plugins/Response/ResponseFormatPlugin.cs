using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace SemanticKernelPoc.Api.Plugins.Response;

/// <summary>
/// Plugin that allows the AI to specify the desired response format for user queries.
/// This helps the AI communicate whether it wants to return structured cards or natural language.
/// </summary>
public class ResponseFormatPlugin
{
    [KernelFunction("indicate_card_response")]
    [Description("Call this function when you want to return structured card data (tasks, emails, calendar events, or SharePoint sites) that should be displayed as interactive cards in the UI. Use this for queries like 'show my tasks', 'get my emails', 'find SharePoint sites', etc.")]
    public string IndicateCardResponse(
        [Description("Type of cards to display: 'tasks', 'emails', 'calendar', 'sharepoint'")] string cardType,
        [Description("Brief explanation of what data will be shown")] string description)
    {
        return $"DISPLAY_CARDS:{cardType}|{description}";
    }

    [KernelFunction("indicate_text_response")]
    [Description("Call this function when you want to return a natural language text response without structured cards. Use this for analysis, summaries, explanations, or when the user asks for insights rather than raw data.")]
    public string IndicateTextResponse(
        [Description("Brief explanation of what type of text response will be provided")] string responseType)
    {
        return $"DISPLAY_TEXT:{responseType}";
    }

    [KernelFunction("indicate_mixed_response")]
    [Description("Call this function when you want to provide both cards and explanatory text. Use this when showing data cards but also providing analysis or context.")]
    public string IndicateMixedResponse(
        [Description("Type of cards to display: 'tasks', 'emails', 'calendar', 'sharepoint'")] string cardType,
        [Description("Type of additional text/analysis to provide")] string textType)
    {
        return $"DISPLAY_MIXED:{cardType}|{textType}";
    }
} 