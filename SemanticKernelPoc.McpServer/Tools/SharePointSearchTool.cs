using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SemanticKernelPoc.McpServer.Services;
using System.ComponentModel;

namespace SemanticKernelPoc.McpServer.Tools;

[McpServerToolType]
public class SharePointSearchTool
{
    private readonly ISharePointSearchService _sharePointSearchService;
    private readonly ILogger<SharePointSearchTool> _logger;

    public SharePointSearchTool(ISharePointSearchService sharePointSearchService, ILogger<SharePointSearchTool> logger)
    {
        _sharePointSearchService = sharePointSearchService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Search for CoffeeNet (CN365) sites in SharePoint using the CN365TemplateId property. These are special workspace sites identified by having a non-empty CN365TemplateId.")]
    public async Task<string> SearchCoffeeNetSites(
        [Description("User access token for authentication")] string userToken,
        [Description("Optional text search query to filter sites by title or description")] string query = null,
        [Description("Optional filter for sites created after this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ)")] string createdAfter = null,
        [Description("Optional filter for sites created before this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ)")] string createdBefore = null,
        [Description("Maximum number of results to return (default: 50, max: 500)")] int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User token is required for authentication.";
            }

            _logger.LogInformation("Searching CoffeeNet sites with query: {Query}, createdAfter: {CreatedAfter}, createdBefore: {CreatedBefore}, maxResults: {MaxResults}",
                query, createdAfter, createdBefore, maxResults);

            // Validate and constrain max results
            maxResults = Math.Min(Math.Max(maxResults, 1), 500);

            // Create search request
            var searchRequest = new SharePointSearchRequest
            {
                Query = query ?? string.Empty,
                CreatedAfter = createdAfter,
                CreatedBefore = createdBefore,
                MaxResults = maxResults
            };

            var searchResponse = await _sharePointSearchService.SearchCoffeeNetSitesAsync(searchRequest, userToken);

            var result = FormatSearchResults(searchResponse);

            _logger.LogInformation("CoffeeNet sites search completed successfully. Found {Count} sites.", searchResponse.Sites.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites");
            return $"Error searching CoffeeNet sites: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for recently created CoffeeNet sites (created within the last specified number of days).")]
    public async Task<string> SearchRecentCoffeeNetSites(
        [Description("User access token for authentication")] string userToken,
        [Description("Optional text search query to filter sites")] string query = null,
        [Description("Number of days to look back (default: 30)")] int daysBack = 30)
    {
        try
        {
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User token is required for authentication.";
            }

            var createdAfter = DateTime.UtcNow.AddDays(-Math.Abs(daysBack)).ToString("yyyy-MM-dd");

            _logger.LogInformation("Searching for recent CoffeeNet sites created after {CreatedAfter}", createdAfter);

            var searchRequest = new SharePointSearchRequest
            {
                Query = query ?? string.Empty,
                CreatedAfter = createdAfter,
                MaxResults = 50
            };

            var searchResponse = await _sharePointSearchService.SearchCoffeeNetSitesAsync(searchRequest, userToken);

            return FormatSearchResults(searchResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent CoffeeNet sites");
            return $"Error searching recent CoffeeNet sites: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Find CoffeeNet sites that match specific keywords in their title or description.")]
    public async Task<string> FindCoffeeNetSitesByKeyword(
        [Description("User access token for authentication")] string userToken,
        [Description("Keywords to search for in site titles and descriptions")] string keywords,
        [Description("Maximum number of results to return (default: 20, max: 500)")] int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User token is required for authentication.";
            }

            if (string.IsNullOrWhiteSpace(keywords))
            {
                return "Please provide keywords to search for.";
            }

            _logger.LogInformation("Searching CoffeeNet sites by keywords: {Keywords}", keywords);

            var searchRequest = new SharePointSearchRequest
            {
                Query = keywords,
                MaxResults = Math.Min(maxResults, 500)
            };

            var searchResponse = await _sharePointSearchService.SearchCoffeeNetSitesAsync(searchRequest, userToken);

            return FormatSearchResults(searchResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites by keyword");
            return $"Error searching CoffeeNet sites by keyword: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Advanced search for CoffeeNet (CN365) sites in SharePoint with intelligent parsing of user intent including time periods, keywords, sorting, and search scope options.")]
    public async Task<string> SearchCoffeeNetSitesAdvanced(
        [Description("User access token for authentication")] string userToken,
        [Description("Natural language search query that will be intelligently parsed for time periods and keywords")] string query = null,
        [Description("Specific time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', 'this_quarter', 'last_quarter', 'this_year', 'last_year', or patterns like 'last_7_days'")] string timePeriod = null,
        [Description("List of specific keywords to search for")] List<string> keywords = null,
        [Description("Search scope: 'title', 'description', 'title_and_description', or 'all' (default)")] string searchScope = "all",
        [Description("Sort by: 'relevance', 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Sort order: 'desc' or 'asc'")] string sortOrder = "desc",
        [Description("Whether to use exact phrase matching")] bool exactMatch = false,
        [Description("Maximum number of results to return (default: 20, max: 500)")] int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User token is required for authentication.";
            }

            _logger.LogInformation("Advanced CoffeeNet search - Query: {Query}, TimePeriod: {TimePeriod}, Keywords: {Keywords}, Scope: {SearchScope}, Sort: {SortBy} {SortOrder}",
                query, timePeriod, string.Join(",", keywords ?? new List<string>()), searchScope, sortBy, sortOrder);

            // Validate and constrain max results
            maxResults = Math.Min(Math.Max(maxResults, 1), 500);

            // Create enhanced search request
            var searchRequest = new SharePointSearchRequest
            {
                Query = query ?? string.Empty,
                TimePeriod = timePeriod ?? string.Empty,
                Keywords = keywords ?? new List<string>(),
                SearchScope = searchScope ?? "all",
                SortBy = sortBy ?? "relevance",
                SortOrder = sortOrder ?? "desc",
                ExactMatch = exactMatch,
                MaxResults = maxResults
            };

            var searchResponse = await _sharePointSearchService.SearchCoffeeNetSitesAsync(searchRequest, userToken);

            var result = FormatSearchResults(searchResponse);

            _logger.LogInformation("Advanced CoffeeNet search completed successfully. Found {Count} sites.", searchResponse.Sites.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced CoffeeNet search");
            return $"Error in advanced search: {ex.Message}";
        }
    }

    private string FormatSearchResults(SharePointSearchResponse response)
    {
        if (!response.Sites.Any())
        {
            return "No CoffeeNet sites found matching the search criteria.";
        }

        var result = $"Found {response.Sites.Count} CoffeeNet sites";
        if (response.HasMore)
        {
            result += $" (showing first {response.Sites.Count} of {response.TotalResults} total results)";
        }
        result += ":\n\n";

        foreach (var site in response.Sites)
        {
            result += $"**{site.Title}**\n";
            result += $"URL: {site.Url}\n";
            result += $"Created: {site.Created:yyyy-MM-dd}\n";
            result += $"CN365 Template ID: {site.TemplateId}\n";

            if (!string.IsNullOrEmpty(site.Description))
            {
                result += $"Description: {site.Description}\n";
            }

            if (!string.IsNullOrEmpty(site.WebTemplate))
            {
                result += $"Web Template: {site.WebTemplate}\n";
            }

            result += "\n---\n\n";
        }

        return result;
    }
}