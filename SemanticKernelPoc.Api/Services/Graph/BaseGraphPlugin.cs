using Microsoft.Graph;
using Microsoft.SemanticKernel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Helpers;

namespace SemanticKernelPoc.Api.Services.Graph;

public abstract class BaseGraphPlugin
{
    protected readonly IGraphService _graphService;
    protected readonly IGraphClientFactory _graphClientFactory;
    protected readonly ILogger _logger;

    protected BaseGraphPlugin(IGraphService graphService, IGraphClientFactory graphClientFactory, ILogger logger)
    {
        _graphService = graphService;
        _graphClientFactory = graphClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Execute a Graph operation with standardized error handling
    /// </summary>
    protected async Task<string> ExecuteGraphOperationAsync<T>(
        Kernel kernel,
        Func<GraphServiceClient, string, Task<T>> operation,
        Func<T, string, string> formatResponse,
        string operationName)
    {
        try
        {
            var (IsValid, ErrorMessage, Client, UserName) = await _graphService.ValidateUserAccessAsync(kernel);
            if (!IsValid)
            {
                return ErrorMessage ?? "Authentication failed";
            }

            _logger.LogDebug("Executing {OperationName} for user {UserName}", operationName, UserName);

            var result = await operation(Client!, UserName);
            var response = formatResponse(result, UserName);

            _logger.LogDebug("Successfully completed {OperationName} for user {UserName}", operationName, UserName);
            return response;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
        {
            _logger.LogError(odataEx, "Microsoft Graph API error in {OperationName}: {ErrorCode}", operationName, odataEx.Error?.Code);
            return $"❌ Microsoft Graph API Error in {operationName}:\n" +
                   $"Error Code: {odataEx.Error?.Code}\n" +
                   $"Error Message: {odataEx.Error?.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {OperationName}", operationName);
            return $"❌ Error in {operationName}: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a simple Graph operation that returns a string directly
    /// </summary>
    protected async Task<string> ExecuteGraphOperationAsync(
        Kernel kernel,
        Func<GraphServiceClient, string, Task<string>> operation,
        string operationName)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            operation,
            (result, userName) => result,
            operationName);
    }

    /// <summary>
    /// Get authenticated Graph client and user info with standardized validation
    /// </summary>
    protected async Task<(bool Success, string ErrorMessage, GraphServiceClient Client, string UserName)> 
        GetAuthenticatedGraphClientAsync(Kernel kernel)
    {
        var (accessToken, userName) = KernelAuthHelper.GetUserAuthInfo(kernel);
        
        if (string.IsNullOrEmpty(accessToken))
        {
            return (false, KernelAuthHelper.GetAuthenticationErrorMessage("access Microsoft Graph"), null!, userName);
        }

        try
        {
            var client = await _graphClientFactory.CreateClientAsync(accessToken);
            return (true, string.Empty, client, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph client for user {UserName}", userName);
            return (false, $"❌ Error creating Graph client: {ex.Message}", null!, userName);
        }
    }

    /// <summary>
    /// Set structured data for card display (non-analysis mode)
    /// </summary>
    protected void SetStructuredDataForCards(Kernel kernel, string dataType, object cardData, int count, string functionResponse = null)
    {
        KernelAuthHelper.SetStructuredData(kernel, dataType, cardData, count, functionResponse);
    }

    /// <summary>
    /// Handle analysis vs card mode responses
    /// </summary>
    protected string HandleAnalysisOrCardResponse(
        Kernel kernel, 
        bool analysisMode, 
        string dataType,
        object cardData, 
        int count, 
        string analysisText,
        string functionResponse = null)
    {
        if (analysisMode)
        {
            // For analysis mode, don't store structured data - return text only
            return analysisText;
        }
        else
        {
            // For card display mode, store structured data
            SetStructuredDataForCards(kernel, dataType, cardData, count, functionResponse);
            return functionResponse ?? $"Found {count} {dataType}. Details are displayed in the cards below.";
        }
    }

    /// <summary>
    /// Format a collection as JSON with user context
    /// </summary>
    protected string FormatJsonResponse<T>(IEnumerable<T> items, string userName, string itemType, int? totalCount = null)
    {
        var itemList = items.ToList();
        var countText = totalCount.HasValue ? $"({itemList.Count} of {totalCount} total)" : $"({itemList.Count} found)";

        return $"{itemType} for {userName} {countText}:\n" +
               JsonSerializer.Serialize(itemList, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Validate required parameters
    /// </summary>
    protected string ValidateRequiredParameter(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{parameterName} cannot be empty. Please provide a valid {parameterName.ToLower()}.";
        }
        return null;
    }

    /// <summary>
    /// Create a success response with consistent formatting
    /// </summary>
    protected string CreateSuccessResponse(string operation, string userName, params (string Label, string Value)[] details)
    {
        var response = $"✅ {operation} successful for {userName}!\n";
        foreach (var (label, value) in details)
        {
            if (!string.IsNullOrEmpty(value))
            {
                response += $"{label}: {value}\n";
            }
        }
        return response.TrimEnd();
    }

    /// <summary>
    /// Get standard authentication error message for specific operation
    /// </summary>
    protected string GetAuthErrorMessage(string operation, string userName = null)
    {
        return KernelAuthHelper.GetAuthenticationErrorMessage(operation, userName);
    }
}