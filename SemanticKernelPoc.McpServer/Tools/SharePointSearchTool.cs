using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SemanticKernelPoc.McpServer.Services;
using System.ComponentModel;
using System.Text.Json;

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
    [Description("Search for SharePoint sites using various filters and criteria. Find sites by title, description, creation date, and other properties.")]
    public async Task<string> SearchSharePointSites(
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

            _logger.LogInformation("Searching SharePoint sites with query: {Query}, createdAfter: {CreatedAfter}, createdBefore: {CreatedBefore}, maxResults: {MaxResults}",
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

            var searchResponse = await _sharePointSearchService.SearchSharePointSitesAsync(searchRequest, userToken);

            var result = FormatSearchResults(searchResponse);

            _logger.LogInformation("SharePoint sites search completed successfully. Found {Count} sites.", searchResponse.Sites.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SharePoint sites");
            return $"Error searching SharePoint sites: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for recently created SharePoint sites (created within the last specified number of days).")]
    public async Task<string> SearchRecentSharePointSites(
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

            _logger.LogInformation("Searching for recent SharePoint sites created after {CreatedAfter}", createdAfter);

            var searchRequest = new SharePointSearchRequest
            {
                Query = query ?? string.Empty,
                CreatedAfter = createdAfter,
                MaxResults = 50
            };

            var searchResponse = await _sharePointSearchService.SearchSharePointSitesAsync(searchRequest, userToken);

            return FormatSearchResults(searchResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent SharePoint sites");
            return $"Error searching recent SharePoint sites: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Find SharePoint sites that match specific keywords in their title or description.")]
    public async Task<string> FindSharePointSitesByKeyword(
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

            _logger.LogInformation("Searching SharePoint sites by keywords: {Keywords}", keywords);

            var searchRequest = new SharePointSearchRequest
            {
                Query = keywords,
                MaxResults = Math.Min(maxResults, 500)
            };

            var searchResponse = await _sharePointSearchService.SearchSharePointSitesAsync(searchRequest, userToken);

            return FormatSearchResults(searchResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SharePoint sites by keyword");
            return $"Error searching SharePoint sites by keyword: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Advanced search for SharePoint sites with intelligent parsing of user intent including time periods, keywords, sorting, and search scope options.")]
    public async Task<string> SearchSharePointSitesAdvanced(
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

            _logger.LogInformation("Advanced SharePoint search - Query: {Query}, TimePeriod: {TimePeriod}, Keywords: {Keywords}, Scope: {SearchScope}, Sort: {SortBy} {SortOrder}",
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

            var searchResponse = await _sharePointSearchService.SearchSharePointSitesAsync(searchRequest, userToken);

            var result = FormatSearchResults(searchResponse);

            _logger.LogInformation("Advanced SharePoint search completed successfully. Found {Count} sites.", searchResponse.Sites.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced SharePoint search");
            return $"Error in advanced search: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Check the status of the MCP server and SharePoint connectivity")]
    public async Task<string> CheckMcpServerStatus(
        [Description("User access token for authentication (optional for basic status)")] string userToken = null)
    {
        try
        {
            var status = new
            {
                McpServerStatus = "Running",
                SharePointConnectivity = "Unknown",
                Timestamp = DateTime.UtcNow,
                AvailableFunctions = new[]
                {
                    "SearchSharePointSites - Search for SharePoint sites using various filters",
                    "SearchRecentSharePointSites - Find recently created SharePoint sites", 
                    "FindSharePointSitesByKeyword - Search SharePoint sites by keywords",
                    "SearchSharePointSitesAdvanced - Advanced SharePoint site search with filters",
                    "CheckMcpServerStatus - Check server status"
                }
            };

            if (!string.IsNullOrEmpty(userToken))
            {
                try
                {
                    // Test connectivity by trying to get tenant info
                    var tenantInfo = await _sharePointSearchService.GetTenantInfoFromGraphAsync(userToken);
                    status = status with { SharePointConnectivity = "Connected" };
                }
                catch (Exception ex)
                {
                    status = status with { SharePointConnectivity = $"Failed: {ex.Message}" };
                }
            }

            _logger.LogInformation("MCP Server status check completed. SharePoint connectivity: {Connectivity}", 
                status.SharePointConnectivity);

            return System.Text.Json.JsonSerializer.Serialize(status, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MCP server status");
            return $"Error checking server status: {ex.Message}";
        }
    }

    private string FormatSearchResults(SharePointSearchResponse response)
    {
        if (!response.Sites.Any())
        {
            return "No SharePoint sites found matching the search criteria.";
        }

        // Format as JSON cards for the frontend
        var sharePointCards = response.Sites.Select(site => new
        {
            title = site.Title,
            url = site.Url,
            created = site.Created.ToString("yyyy-MM-dd"),
            webTemplate = site.WebTemplate,
            description = site.Description
        }).ToList();

        var result = System.Text.Json.JsonSerializer.Serialize(sharePointCards, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        return $"SHAREPOINT_CARDS: {result}";
    }
}