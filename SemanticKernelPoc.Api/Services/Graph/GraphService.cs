using Microsoft.Graph;
using Microsoft.SemanticKernel;

namespace SemanticKernelPoc.Api.Services.Graph;

public class GraphService : IGraphService
{
    private readonly ILogger<GraphService> _logger;

    public GraphService(ILogger<GraphService> logger)
    {
        _logger = logger;
    }

    public GraphServiceClient CreateClient(string userAccessToken)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userAccessToken);
        
        return new GraphServiceClient(httpClient);
    }

    public (string AccessToken, string UserName, string UserId) GetUserContext(Kernel kernel)
    {
        var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : "";
        var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";
        var userId = kernel.Data.TryGetValue("UserId", out var id) ? id?.ToString() : "";

        return (userAccessToken, userName, userId);
    }

    public async Task<(bool IsValid, string ErrorMessage, GraphServiceClient Client, string UserName)> ValidateUserAccessAsync(Kernel kernel)
    {
        var (accessToken, userName, userId) = GetUserContext(kernel);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("User authentication required for user {UserId}", userId);
            return (false, "Unable to access Microsoft 365 services - user authentication required.", null, userName);
        }

        try
        {
            var client = CreateClient(accessToken);
            
            // Test the connection by getting user profile
            var user = await client.Me.GetAsync();
            _logger.LogDebug("Successfully validated access for user {UserId} ({UserName})", userId, user?.DisplayName ?? userName);
            
            return (true, null, client, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Microsoft Graph access for user {UserId}", userId);
            return (false, $"Unable to access Microsoft Graph - authentication may have expired: {ex.Message}", null, userName);
        }
    }
} 