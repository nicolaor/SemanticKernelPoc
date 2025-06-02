using Microsoft.SemanticKernel;

namespace SemanticKernelPoc.Api.Services.Helpers;

/// <summary>
/// Helper class for common kernel authentication operations
/// </summary>
public static class KernelAuthHelper
{
    /// <summary>
    /// Extracts user authentication information from kernel data
    /// </summary>
    /// <param name="kernel">The kernel instance</param>
    /// <returns>Tuple containing access token and user name</returns>
    public static (string AccessToken, string UserName) GetUserAuthInfo(Kernel kernel)
    {
        var accessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) 
            ? token?.ToString() 
            : null;
            
        var userName = kernel.Data.TryGetValue("UserName", out var name) 
            ? name?.ToString() 
            : "User";
            
        return (accessToken ?? string.Empty, userName);
    }
    
    /// <summary>
    /// Gets a standardized authentication error message
    /// </summary>
    /// <param name="operation">The operation that failed</param>
    /// <param name="userName">The user name (optional)</param>
    /// <returns>Formatted error message</returns>
    public static string GetAuthenticationErrorMessage(string operation, string userName = null)
    {
        var userPart = !string.IsNullOrEmpty(userName) ? $" for {userName}" : "";
        return $"Unable to {operation.ToLower()}{userPart} - user authentication required.";
    }
    
    /// <summary>
    /// Validates that user access token exists
    /// </summary>
    /// <param name="kernel">The kernel instance</param>
    /// <returns>True if valid token exists</returns>
    public static bool HasValidUserToken(Kernel kernel)
    {
        var (accessToken, _) = GetUserAuthInfo(kernel);
        return !string.IsNullOrEmpty(accessToken);
    }
    
    /// <summary>
    /// Sets structured data in kernel for response processing
    /// </summary>
    /// <param name="kernel">The kernel instance</param>
    /// <param name="type">The data type (tasks, emails, calendar, sharepoint)</param>
    /// <param name="data">The structured data object</param>
    /// <param name="count">Number of items</param>
    /// <param name="functionResponse">Optional function response text</param>
    public static void SetStructuredData(Kernel kernel, string type, object data, int count, string functionResponse = null)
    {
        kernel.Data[$"{CapitalizeFirst(type)}Cards"] = data;
        kernel.Data["HasStructuredData"] = "true";
        kernel.Data["StructuredDataType"] = type.ToLower();
        kernel.Data["StructuredDataCount"] = count;
        
        if (!string.IsNullOrEmpty(functionResponse))
        {
            kernel.Data[$"{CapitalizeFirst(type)}FunctionResponse"] = functionResponse;
        }
    }
    
    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }
} 