using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Services;
using System.ComponentModel;

namespace SemanticKernelPoc.Api.Plugins.SharePoint;

/// <summary>
/// SharePoint plugin that uses Model Context Protocol (MCP) to search for CoffeeNet sites.
/// This plugin provides AI-powered search capabilities for SharePoint sites with CN365TemplateId property.
/// </summary>
public class SharePointMcpPlugin
{
    private readonly IMcpClientService _mcpClientService;
    private readonly ILogger<SharePointMcpPlugin> _logger;

    public SharePointMcpPlugin(IMcpClientService mcpClientService, ILogger<SharePointMcpPlugin> logger)
    {
        _mcpClientService = mcpClientService;
        _logger = logger;
    }

    [KernelFunction("search_coffeenet_sites")]
    [Description("Search for CoffeeNet (CN365) sites in SharePoint. These are special workspace sites identified by the CN365TemplateId property.")]
    public async Task<string> SearchCoffeeNetSitesAsync(
        Kernel kernel,
        [Description("Optional text search query to filter sites by title or description")] string query = null,
        [Description("Optional filter for sites created after this date (format: yyyy-MM-dd)")] string createdAfter = null,
        [Description("Optional filter for sites created before this date (format: yyyy-MM-dd)")] string createdBefore = null,
        [Description("Maximum number of results to return (default: 50, max: 500)")] int maxResults = 50)
    {
        try
        {
            // Extract user access token from kernel context
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            
            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Error: User authentication required. Please ensure you are logged in.";
            }

            _logger.LogInformation("Searching CoffeeNet sites with query: {Query}, createdAfter: {CreatedAfter}, createdBefore: {CreatedBefore}, maxResults: {MaxResults}", 
                query, createdAfter, createdBefore, maxResults);

            // Validate max results
            maxResults = Math.Min(Math.Max(maxResults, 1), 500);

            var result = await _mcpClientService.SearchCoffeeNetSitesAsync(
                userAccessToken,
                query, 
                createdAfter, 
                createdBefore, 
                maxResults);
            
            _logger.LogInformation("CoffeeNet sites search completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites");
            return $"Error searching CoffeeNet sites: {ex.Message}";
        }
    }

    [KernelFunction("check_mcp_server_status")]
    [Description("Check if the MCP server for SharePoint search is available and running.")]
    public async Task<string> CheckMcpServerStatusAsync()
    {
        try
        {
            var isAvailable = await _mcpClientService.IsServerRunningAsync();
            return isAvailable 
                ? "MCP server is running and available for SharePoint search operations." 
                : "MCP server is not available. SharePoint search functionality may not work.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MCP server status");
            return $"Error checking MCP server status: {ex.Message}";
        }
    }

    [KernelFunction("search_recent_coffeenet_sites")]
    [Description("Search for recently created CoffeeNet sites (created in the last 30 days).")]
    public async Task<string> SearchRecentCoffeeNetSitesAsync(
        Kernel kernel,
        [Description("Optional text search query to filter sites")] string query = null,
        [Description("Number of days to look back (default: 30)")] int daysBack = 30)
    {
        try
        {
            // Extract user access token from kernel context
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            
            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Error: User authentication required. Please ensure you are logged in.";
            }
            
            _logger.LogInformation("Searching for recent CoffeeNet sites for last {DaysBack} days", daysBack);
            
            var result = await _mcpClientService.SearchRecentCoffeeNetSitesAsync(
                userAccessToken,
                query, 
                daysBack);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent CoffeeNet sites");
            return $"Error searching recent CoffeeNet sites: {ex.Message}";
        }
    }

    [KernelFunction("find_coffeenet_sites_by_keyword")]
    [Description("Find CoffeeNet sites that match specific keywords in their title or description.")]
    public async Task<string> FindCoffeeNetSitesByKeywordAsync(
        Kernel kernel,
        [Description("Keywords to search for in site titles and descriptions")] string keywords,
        [Description("Maximum number of results to return (default: 20)")] int maxResults = 20)
    {
        try
        {
            // Extract user access token from kernel context
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            
            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Error: User authentication required. Please ensure you are logged in.";
            }

            if (string.IsNullOrWhiteSpace(keywords))
            {
                return "Please provide keywords to search for.";
            }

            _logger.LogInformation("Searching CoffeeNet sites by keywords: {Keywords}", keywords);
            
            var result = await _mcpClientService.FindCoffeeNetSitesByKeywordAsync(userAccessToken, keywords, Math.Min(maxResults, 500));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites by keyword");
            return $"Error searching CoffeeNet sites by keyword: {ex.Message}";
        }
    }

    [KernelFunction("search_coffeenet_sites_advanced")]
    [Description("Advanced search for CoffeeNet sites with multiple filter options and natural language processing.")]
    public async Task<string> SearchCoffeeNetSitesAdvancedAsync(
        Kernel kernel,
        [Description("Natural language search query (e.g., 'find marketing sites created last month')")] string naturalQuery,
        [Description("Optional specific keywords to include")] string keywords = null,
        [Description("Optional date range: 'last_week', 'last_month', 'last_quarter', or specific date")] string dateRange = null,
        [Description("Maximum number of results (default: 20)")] int maxResults = 20)
    {
        try
        {
            // Extract user access token from kernel context
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            
            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Error: User authentication required. Please ensure you are logged in.";
            }

            _logger.LogInformation("Advanced CoffeeNet search with natural query: {Query}", naturalQuery);

            // Parse natural language date ranges
            string createdAfter = null;
            if (!string.IsNullOrEmpty(dateRange))
            {
                var parsedDate = dateRange.ToLowerInvariant() switch
                {
                    "last_week" => DateTime.UtcNow.AddDays(-7),
                    "last_month" => DateTime.UtcNow.AddMonths(-1),
                    "last_quarter" => DateTime.UtcNow.AddMonths(-3),
                    "last_year" => DateTime.UtcNow.AddYears(-1),
                    _ when DateTime.TryParse(dateRange, out var date) => date,
                    _ => (DateTime?)null
                };
                
                createdAfter = parsedDate?.ToString("yyyy-MM-dd");
            }

            // Combine natural query with keywords
            var searchQuery = string.IsNullOrEmpty(keywords) 
                ? naturalQuery 
                : $"{naturalQuery} {keywords}";

            var result = await _mcpClientService.SearchCoffeeNetSitesAsync(
                userAccessToken,
                searchQuery,
                createdAfter,
                null,
                Math.Min(maxResults, 500));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced CoffeeNet search");
            return $"Error in advanced search: {ex.Message}";
        }
    }
} 