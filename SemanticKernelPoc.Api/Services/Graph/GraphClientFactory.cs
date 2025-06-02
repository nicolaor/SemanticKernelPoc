using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Collections.Concurrent;
using System.Net.Http;

namespace SemanticKernelPoc.Api.Services.Graph;

public interface IGraphClientFactory
{
    Task<GraphServiceClient> CreateClientAsync(string accessToken);
    void ClearCache(); // For testing or cleanup
}

public class GraphClientFactory : IGraphClientFactory, IDisposable
{
    private readonly ILogger<GraphClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, (GraphServiceClient Client, DateTime LastUsed)> _clientCache;
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new object();
    private const int CacheExpirationMinutes = 30;

    public GraphClientFactory(ILogger<GraphClientFactory> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _clientCache = new ConcurrentDictionary<string, (GraphServiceClient, DateTime)>();
        
        // Setup cleanup timer to run every 15 minutes
        _cleanupTimer = new Timer(CleanupExpiredClients, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }

    public async Task<GraphServiceClient> CreateClientAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new ArgumentException("Access token cannot be null or empty", nameof(accessToken));
        }

        // Create a hash of the token for caching (for security, don't store full token)
        var tokenHash = accessToken.GetHashCode().ToString();

        lock (_lockObject)
        {
            // Check if we have a cached client for this token
            if (_clientCache.TryGetValue(tokenHash, out var cachedEntry))
            {
                // Update last used time and return cached client
                _clientCache[tokenHash] = (cachedEntry.Client, DateTime.UtcNow);
                _logger.LogDebug("Returning cached Graph client for token hash {TokenHash}", tokenHash);
                return cachedEntry.Client;
            }
        }

        // Create new client using the same pattern as GraphService
        _logger.LogDebug("Creating new Graph client for token hash {TokenHash}", tokenHash);
        
        try
        {
            var graphToken = await GetGraphTokenAsync(accessToken);

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", graphToken);

            var graphClient = new GraphServiceClient(httpClient);

            // Cache the client
            lock (_lockObject)
            {
                _clientCache[tokenHash] = (graphClient, DateTime.UtcNow);
                _logger.LogDebug("Cached new Graph client. Total cached clients: {Count}", _clientCache.Count);
            }

            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph client");
            throw;
        }
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

            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Graph API token using On-Behalf-Of flow: {Error}", ex.Message);
            throw;
        }
    }

    public void ClearCache()
    {
        lock (_lockObject)
        {
            var count = _clientCache.Count;
            _clientCache.Clear();
            _logger.LogInformation("Cleared Graph client cache. Removed {Count} clients", count);
        }
    }

    private void CleanupExpiredClients(object state)
    {
        lock (_lockObject)
        {
            var expiredKeys = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-CacheExpirationMinutes);

            foreach (var kvp in _clientCache)
            {
                if (kvp.Value.LastUsed < cutoffTime)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                if (_clientCache.TryRemove(key, out var removedEntry))
                {
                    try
                    {
                        removedEntry.Client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing expired Graph client for key {Key}", key);
                    }
                }
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {ExpiredCount} expired Graph clients. Remaining: {RemainingCount}", 
                    expiredKeys.Count, _clientCache.Count);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        lock (_lockObject)
        {
            foreach (var kvp in _clientCache.Values)
            {
                try
                {
                    kvp.Client?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing Graph client during factory disposal");
                }
            }
            _clientCache.Clear();
        }
    }
} 