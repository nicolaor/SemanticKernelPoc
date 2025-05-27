using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace SemanticKernelPoc.Api.Services;

public interface IMcpClientService
{
    Task<string> SearchCoffeeNetSitesAsync(string userToken, string query = null, string createdAfter = null, string createdBefore = null, int maxResults = 50);
    Task<string> SearchRecentCoffeeNetSitesAsync(string userToken, string query = null, int daysBack = 30);
    Task<string> FindCoffeeNetSitesByKeywordAsync(string userToken, string keywords, int maxResults = 20);
    Task<bool> IsServerRunningAsync();
}

public class McpClientService : IMcpClientService, IDisposable
{
    private readonly ILogger<McpClientService> _logger;
    private readonly string _mcpServerUrl;
    private readonly IWebHostEnvironment _environment;
    private IMcpClient _mcpClient;
    private bool _disposed = false;
    private static bool _sslBypassConfigured = false;

    public McpClientService(ILogger<McpClientService> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
        _mcpServerUrl = configuration["McpServer:Url"] ?? "http://localhost:31339";
        
        // Configure SSL bypass for development (only once)
        if (_environment.IsDevelopment() && !_sslBypassConfigured && _mcpServerUrl.Contains("localhost"))
        {
            ConfigureSslBypass();
            _sslBypassConfigured = true;
        }
    }

    private void ConfigureSslBypass()
    {
        // For development only: bypass SSL certificate validation for localhost
        ServicePointManager.ServerCertificateValidationCallback = 
            (sender, certificate, chain, sslPolicyErrors) =>
            {
                // Allow any certificate for localhost in development
                if (sender is HttpWebRequest request && 
                    (request.RequestUri.Host == "localhost" || request.RequestUri.Host == "127.0.0.1"))
                {
                    return true;
                }
                
                // For other hosts, use default validation
                return sslPolicyErrors == SslPolicyErrors.None;
            };
    }

    public async Task<string> SearchCoffeeNetSitesAsync(string userToken, string query = null, string createdAfter = null, string createdBefore = null, int maxResults = 50)
    {
        try
        {
            await EnsureClientConnectedAsync();
            
            var result = await _mcpClient.CallToolAsync("SearchCoffeeNetSites", new Dictionary<string, object>
            {
                ["userToken"] = userToken,
                ["query"] = query ?? string.Empty,
                ["createdAfter"] = createdAfter ?? string.Empty,
                ["createdBefore"] = createdBefore ?? string.Empty,
                ["maxResults"] = maxResults
            });

            return result?.Content?.FirstOrDefault()?.Text ?? "No results returned";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SearchCoffeeNetSites tool");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> SearchRecentCoffeeNetSitesAsync(string userToken, string query = null, int daysBack = 30)
    {
        try
        {
            await EnsureClientConnectedAsync();
            
            var result = await _mcpClient.CallToolAsync("SearchRecentCoffeeNetSites", new Dictionary<string, object>
            {
                ["userToken"] = userToken,
                ["query"] = query ?? string.Empty,
                ["daysBack"] = daysBack
            });

            return result?.Content?.FirstOrDefault()?.Text ?? "No results returned";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SearchRecentCoffeeNetSites tool");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> FindCoffeeNetSitesByKeywordAsync(string userToken, string keywords, int maxResults = 20)
    {
        try
        {
            await EnsureClientConnectedAsync();
            
            var result = await _mcpClient.CallToolAsync("FindCoffeeNetSitesByKeyword", new Dictionary<string, object>
            {
                ["userToken"] = userToken,
                ["keywords"] = keywords ?? string.Empty,
                ["maxResults"] = maxResults
            });

            return result?.Content?.FirstOrDefault()?.Text ?? "No results returned";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling FindCoffeeNetSitesByKeyword tool");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsServerRunningAsync()
    {
        try
        {
            if (_mcpClient == null)
            {
                return false;
            }

            // Try to list tools to check if server is responsive
            var tools = await _mcpClient.ListToolsAsync();
            return tools?.Any() == true;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureClientConnectedAsync()
    {
        if (_mcpClient == null)
        {
            await ConnectToServerAsync();
        }
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            _logger.LogInformation("Connecting to MCP server at: {Url}", _mcpServerUrl);

            // Create client transport for SSE communication
            var clientTransport = new SseClientTransport(new SseClientTransportOptions
            {
                Endpoint = new Uri($"{_mcpServerUrl}/sse")
            });

            // Create MCP client using the factory
            _mcpClient = await McpClientFactory.CreateAsync(clientTransport);

            _logger.LogInformation("MCP client connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                if (_mcpClient is IDisposable disposableClient)
                {
                    disposableClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MCP client service");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
} 