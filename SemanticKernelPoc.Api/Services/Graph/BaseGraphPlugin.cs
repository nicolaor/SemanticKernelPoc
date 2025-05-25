using Microsoft.Graph;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace SemanticKernelPoc.Api.Services.Graph;

public abstract class BaseGraphPlugin
{
    protected readonly IGraphService _graphService;
    protected readonly ILogger _logger;

    protected BaseGraphPlugin(IGraphService graphService, ILogger logger)
    {
        _graphService = graphService;
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
            var validation = await _graphService.ValidateUserAccessAsync(kernel);
            if (!validation.IsValid)
            {
                return validation.ErrorMessage ?? "Authentication failed";
            }

            _logger.LogDebug("Executing {OperationName} for user {UserName}", operationName, validation.UserName);

            var result = await operation(validation.Client!, validation.UserName);
            var response = formatResponse(result, validation.UserName);

            _logger.LogDebug("Successfully completed {OperationName} for user {UserName}", operationName, validation.UserName);
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
} 