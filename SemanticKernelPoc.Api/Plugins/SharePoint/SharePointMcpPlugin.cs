using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Services;
using System.ComponentModel;

namespace SemanticKernelPoc.Api.Plugins.SharePoint;

/// <summary>
/// SharePoint plugin that uses Model Context Protocol (MCP) to search for CoffeeNet sites.
/// This plugin provides AI-powered search capabilities for SharePoint sites with CN365TemplateId property.
/// </summary>
public class SharePointMcpPlugin(IMcpClientService mcpClientService, ILogger<SharePointMcpPlugin> logger)
{
    private readonly IMcpClientService _mcpClientService = mcpClientService;
    private readonly ILogger<SharePointMcpPlugin> _logger = logger;

    [KernelFunction("search_coffeenet_sites")]
    [Description("Search for CoffeeNet (CN365) SharePoint sites. Use this function when the user asks to find, list, search, or show SharePoint sites. These are special workspace sites identified by the CN365TemplateId property. Supports intelligent parsing of user intent including time periods, keywords, and sorting preferences.")]
    public async Task<string> SearchCoffeeNetSitesAsync(
        Kernel kernel,
        [Description("Natural language search query (e.g., 'marketing sites created last week', 'find development sites', 'show recent project sites'). The system will intelligently parse time periods, keywords, and intent from this query.")] string query = null,
        [Description("Specific time period filter: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', 'this_quarter', 'last_quarter', 'this_year', 'last_year', or 'last_X_days/weeks/months' (e.g., 'last_7_days', 'last_2_weeks')")] string timePeriod = null,
        [Description("Individual keywords to search for, separated by commas (e.g., 'marketing, campaign, sales')")] string keywords = null,
        [Description("Search scope: 'title' (titles only), 'description' (descriptions only), 'title_and_description', or 'all' (default)")] string searchScope = "all",
        [Description("Sort results by: 'relevance' (default), 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Sort order: 'desc' (default) or 'asc'")] string sortOrder = "desc",
        [Description("Whether to search for exact phrase match")] bool exactMatch = false,
        [Description("Maximum number of results to return (default: 20, max: 500)")] int maxResults = 20)
    {
        try
        {
            // Extract the API access token (the one the client sent to this API)
            var apiAccessToken = kernel.Data.TryGetValue("ApiUserAccessToken", out var token) ? token?.ToString() : null;

            if (string.IsNullOrEmpty(apiAccessToken))
            {
                return "Error: API User authentication token not found. Please ensure you are logged in.";
            }

            _logger.LogInformation("Enhanced CoffeeNet sites search - Query: {Query}, TimePeriod: {TimePeriod}, Keywords: {Keywords}, Scope: {SearchScope}, Sort: {SortBy} {SortOrder}",
                query, timePeriod, keywords, searchScope, sortBy, sortOrder);

            // Validate and constrain parameters
            maxResults = Math.Min(Math.Max(maxResults, 1), 500);
            searchScope = string.IsNullOrEmpty(searchScope) ? "all" : searchScope;
            sortBy = string.IsNullOrEmpty(sortBy) ? "relevance" : sortBy;
            sortOrder = string.IsNullOrEmpty(sortOrder) ? "desc" : sortOrder;

            // Parse keywords
            var keywordList = string.IsNullOrEmpty(keywords) ? 
                new List<string>() : 
                keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(k => k.Trim())
                       .Where(k => !string.IsNullOrEmpty(k))
                       .ToList();

            var result = await _mcpClientService.SearchCoffeeNetSitesAdvancedAsync(
                apiAccessToken,
                query ?? "",
                timePeriod,
                keywordList,
                searchScope,
                sortBy,
                sortOrder,
                exactMatch,
                maxResults);

            _logger.LogInformation("Enhanced CoffeeNet sites search completed successfully. Result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in enhanced CoffeeNet sites search");
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
    [Description("Search for recently created CoffeeNet sites. Use this when users ask for 'recent sites', 'latest sites', 'new sites', or specify a time period like 'sites created this week'. This function automatically handles time-based filtering.")]
    public async Task<string> SearchRecentCoffeeNetSitesAsync(
        Kernel kernel,
        [Description("Optional text search query to filter recent sites")] string query = null,
        [Description("Time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days back (default: 30)")] string timePeriod = "last_month",
        [Description("Sort by: 'created' (default for recent), 'modified', 'title', or 'relevance'")] string sortBy = "created",
        [Description("Maximum number of results (default: 10)")] int maxResults = 10)
    {
        try
        {
            // Extract the API access token
            var apiAccessToken = kernel.Data.TryGetValue("ApiUserAccessToken", out var token) ? token?.ToString() : null;

            if (string.IsNullOrEmpty(apiAccessToken))
            {
                return "Error: API User authentication token not found. Please ensure you are logged in.";
            }

            _logger.LogInformation("Searching for recent CoffeeNet sites - TimePeriod: {TimePeriod}, Query: {Query}", timePeriod, query);

            var result = await _mcpClientService.SearchCoffeeNetSitesAdvancedAsync(
                apiAccessToken,
                query ?? "",
                timePeriod,
                new List<string>(),
                "all",
                sortBy,
                "desc", // Most recent first
                false,
                Math.Min(maxResults, 100));

            _logger.LogInformation("Recent CoffeeNet sites search completed. Result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent CoffeeNet sites");
            return $"Error searching recent CoffeeNet sites: {ex.Message}";
        }
    }

    [KernelFunction("find_coffeenet_sites_by_keyword")]
    [Description("Find CoffeeNet SharePoint sites that match specific keywords in their title or description. Use this function when the user explicitly asks to find SharePoint sites by specific keywords or topics. Supports exact matching and different search scopes.")]
    public async Task<string> FindCoffeeNetSitesByKeywordAsync(
        Kernel kernel,
        [Description("Keywords to search for (can be single keyword or comma-separated list)")] string keywords,
        [Description("Search scope: 'title', 'description', 'title_and_description', or 'all' (default)")] string searchScope = "title_and_description",
        [Description("Whether to search for exact keyword matches")] bool exactMatch = false,
        [Description("Sort results by: 'relevance' (default), 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Maximum number of results to return (default: 20)")] int maxResults = 20)
    {
        try
        {
            // Extract the API access token
            var apiAccessToken = kernel.Data.TryGetValue("ApiUserAccessToken", out var token) ? token?.ToString() : null;

            if (string.IsNullOrEmpty(apiAccessToken))
            {
                return "Error: API User authentication token not found. Please ensure you are logged in.";
            }

            if (string.IsNullOrWhiteSpace(keywords))
            {
                return "Please provide keywords to search for.";
            }

            _logger.LogInformation("Searching CoffeeNet sites by keywords: {Keywords}, Scope: {SearchScope}, ExactMatch: {ExactMatch}", 
                keywords, searchScope, exactMatch);

            // Parse keywords
            var keywordList = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(k => k.Trim())
                                    .Where(k => !string.IsNullOrEmpty(k))
                                    .ToList();

            var result = await _mcpClientService.SearchCoffeeNetSitesAdvancedAsync(
                apiAccessToken,
                "", // No main query, using keywords
                "",
                keywordList,
                searchScope,
                sortBy,
                "desc",
                exactMatch,
                Math.Min(maxResults, 500));

            _logger.LogInformation("Keyword-based CoffeeNet search completed. Result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites by keyword");
            return $"Error searching CoffeeNet sites by keyword: {ex.Message}";
        }
    }

    [KernelFunction("search_coffeenet_sites_advanced")]
    [Description("Advanced search for CoffeeNet SharePoint sites using natural language queries and multiple filter options. Use this for complex SharePoint site search requests that involve multiple criteria or when the user provides detailed search requirements. This function can intelligently parse user intent and apply appropriate filters.")]
    public async Task<string> SearchCoffeeNetSitesAdvancedAsync(
        Kernel kernel,
        [Description("Natural language search query (e.g., 'find marketing sites created last month', 'show development projects from this quarter', 'recent training sites')")] string naturalQuery,
        [Description("Specific time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', 'this_quarter', 'last_quarter', 'this_year', 'last_year', or patterns like 'last_7_days'")] string timePeriod = null,
        [Description("Specific keywords to include (comma-separated)")] string keywords = null,
        [Description("Search scope: 'title', 'description', 'title_and_description', or 'all'")] string searchScope = "all",
        [Description("Sort by: 'relevance', 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Sort order: 'desc' or 'asc'")] string sortOrder = "desc",
        [Description("Whether to use exact phrase matching")] bool exactMatch = false,
        [Description("Maximum number of results (default: 20)")] int maxResults = 20)
    {
        try
        {
            // Extract the API access token
            var apiAccessToken = kernel.Data.TryGetValue("ApiUserAccessToken", out var token) ? token?.ToString() : null;

            if (string.IsNullOrEmpty(apiAccessToken))
            {
                return "Error: API User authentication token not found. Please ensure you are logged in.";
            }

            _logger.LogInformation("Advanced CoffeeNet search - Query: {Query}, TimePeriod: {TimePeriod}, Keywords: {Keywords}", 
                naturalQuery, timePeriod, keywords);

            // Parse keywords if provided
            var keywordList = string.IsNullOrEmpty(keywords) ? 
                new List<string>() : 
                keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(k => k.Trim())
                       .Where(k => !string.IsNullOrEmpty(k))
                       .ToList();

            var result = await _mcpClientService.SearchCoffeeNetSitesAdvancedAsync(
                apiAccessToken,
                naturalQuery ?? "",
                timePeriod,
                keywordList,
                searchScope,
                sortBy,
                sortOrder,
                exactMatch,
                Math.Min(maxResults, 500));

            _logger.LogInformation("Advanced CoffeeNet search completed. Result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced CoffeeNet search");
            return $"Error in advanced search: {ex.Message}";
        }
    }
}