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
    [Description("Search for SharePoint sites using various filters and criteria. Use this when user asks for SharePoint sites, wants to find specific sites, or needs to search by creation date. Ideal for general SharePoint site discovery and when specific search terms or date ranges are provided. For queries asking for 'last N sites' or 'most recent N sites' (where N is a number), use this function with maxResults=N and no time filters to get the N most recently created sites.")]
    public async Task<string> SearchSharePointSites(
        [Description("User access token for authentication")] string userToken,
        [Description("Text search query to filter sites by title or description - use keywords from user's request")] string query = null,
        [Description("Filter for sites created after this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ) - extract from user phrases like 'since January' or 'after last week'")] string createdAfter = null,
        [Description("Filter for sites created before this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ) - extract from user phrases like 'before March' or 'until yesterday'")] string createdBefore = null,
        [Description("Maximum number of results to return (default: 50, max: 500). IMPORTANT: When user asks for 'last N sites' or 'most recent N sites', set this to N to get the N most recently created sites ordered by creation date.")] int maxResults = 50)
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

            // If no time filters are provided but user appears to want recent sites (small maxResults),
            // automatically sort by creation date to get the most recently created sites
            if (string.IsNullOrEmpty(createdAfter) && string.IsNullOrEmpty(createdBefore) && maxResults <= 20)
            {
                searchRequest.SortBy = "created";
                searchRequest.SortOrder = "desc";
                _logger.LogInformation("🔄 Auto-applying creation date sorting for recent sites query (maxResults: {MaxResults})", maxResults);
            }

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
    [Description("Search for recently created SharePoint sites within a specific time period (created within the last specified number of days). Use this ONLY when user specifically mentions time periods like 'last 30 days', 'this month', 'past week', 'sites created in the last 7 days'. Do NOT use this for queries like 'last 3 sites' or 'most recent 5 sites' - those should use SearchSharePointSites with maxResults instead.")]
    public async Task<string> SearchRecentSharePointSites(
        [Description("User access token for authentication")] string userToken,
        [Description("Text search query to filter recent sites - use keywords from user's request")] string query = null,
        [Description("Number of days to look back (default: 30) - interpret from user phrases like 'last week' (7), 'this month' (30), 'past 3 days' (3). Only use this when user specifically mentions a time period, not when they ask for 'last N sites'.")] int daysBack = 30)
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
    [Description("Find SharePoint sites that match specific keywords in their title or description. Use this when user provides specific keywords, names, or terms to search for. Best for targeted searches when user knows what they're looking for.")]
    public async Task<string> FindSharePointSitesByKeyword(
        [Description("User access token for authentication")] string userToken,
        [Description("Keywords to search for in site titles and descriptions - extract key terms from user's question")] string keywords,
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
    [Description("Advanced search for SharePoint sites with intelligent parsing and complex filtering options. Use this for complex queries involving multiple criteria, sorting requirements, or when user asks for advanced search features. Examples: 'find sites sorted by creation date', 'search for exact phrase matches', or queries with multiple filters. IMPORTANT: When user asks for 'last N sites' or 'most recent N sites', use SearchSharePointSites instead with maxResults=N.")]
    public async Task<string> SearchSharePointSitesAdvanced(
        [Description("User access token for authentication")] string userToken,
        [Description("Natural language search query that will be intelligently parsed - include full user request for smart interpretation")] string query = null,
        [Description("Specific time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', 'this_quarter', 'last_quarter', 'this_year', 'last_year', or patterns like 'last_7_days'. Do NOT use for 'last N sites' queries.")] string timePeriod = null,
        [Description("List of specific keywords to search for")] List<string> keywords = null,
        [Description("Search scope: 'title', 'description', 'title_and_description', or 'all' (default)")] string searchScope = "all",
        [Description("Sort by: 'relevance', 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Sort order: 'desc' or 'asc'")] string sortOrder = "desc",
        [Description("Whether to use exact phrase matching")] bool exactMatch = false,
        [Description("Maximum number of results to return (default: 20, max: 500). When user asks for 'last N sites', set this to N and sortBy to 'created' with sortOrder 'desc'.")] int maxResults = 20)
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
    [Description("Check the status and connectivity of the MCP server and SharePoint services. Use this when user asks about server status, connectivity issues, or wants to verify SharePoint access. Also use to troubleshoot authentication problems.")]
    public async Task<string> CheckMcpServerStatus(
        [Description("User access token for authentication (optional for basic status check but required for full SharePoint connectivity test)")] string userToken = null)
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

        // Return JSON directly without prefix since API plugin now uses kernel.Data approach
        var sharePointCards = response.Sites.Select(site => new
        {
            title = site.Title,
            url = site.Url,
            created = site.Created.ToString("yyyy-MM-dd"),
            webTemplate = site.WebTemplate,
            description = site.Description
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(sharePointCards, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
    }
}