using Microsoft.Graph;
using Microsoft.SemanticKernel;
using Microsoft.Identity.Client;

namespace SemanticKernelPoc.Api.Services.Graph;

public class GraphService : IGraphService
{
    private readonly ILogger<GraphService> _logger;
    private readonly IConfiguration _configuration;

    public GraphService(ILogger<GraphService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<GraphServiceClient> CreateClientAsync(string userAccessToken)
    {
        try
        {
            _logger.LogInformation("Creating Graph client using On-Behalf-Of flow");
            var graphToken = await GetGraphTokenAsync(userAccessToken);

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", graphToken);

            return new GraphServiceClient(httpClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph client using On-Behalf-Of flow");
            throw;
        }
    }

    public GraphServiceClient CreateClient(string userAccessToken)
    {
        // Legacy synchronous method - use the async version when possible
        return CreateClientAsync(userAccessToken).GetAwaiter().GetResult();
    }

    private async Task<string> GetGraphTokenAsync(string userAccessToken)
    {
        try
        {
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];
            var tenantId = _configuration["AzureAd:TenantId"];

            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var userAssertion = new UserAssertion(userAccessToken);

            var result = await app.AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync();

            _logger.LogInformation("Successfully acquired Graph API token using On-Behalf-Of flow");
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Graph API token using On-Behalf-Of flow: {Error}", ex.Message);
            throw;
        }
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
            var client = await CreateClientAsync(accessToken);

            // Test the connection by getting user profile
            var user = await client.Me.GetAsync();
            _logger.LogDebug("Successfully validated access for user {UserId} ({UserName})", userId, user?.DisplayName ?? userName);

            return (true, null, client, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Microsoft Graph access for user {UserId}: {Error}", userId, ex.Message);
            return (false, $"Unable to access Microsoft Graph - authentication may have expired: {ex.Message}", null, userName);
        }
    }
}