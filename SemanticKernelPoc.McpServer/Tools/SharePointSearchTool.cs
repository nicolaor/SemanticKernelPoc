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
    private readonly ISharePointSearchService _sharePointService;
    private readonly ILogger<SharePointSearchTool> _logger;

    public SharePointSearchTool(ISharePointSearchService sharePointService, ILogger<SharePointSearchTool> logger)
    {
        _sharePointService = sharePointService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Simple test with no dependencies to verify MCP execution")]
    public string SimpleTest([Description("User access token for testing")] string accessToken = "")
    {
        _logger.LogInformation("Simple test method called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
        return $"Simple test method executed successfully - accessToken length: {accessToken?.Length ?? 0}";
    }

    [McpServerTool]
    [Description("Search for SharePoint sites using various filters and criteria. Use this when user asks for SharePoint sites, wants to find specific sites, or needs to search by creation date. Ideal for general SharePoint site discovery and when specific search terms or date ranges are provided. For queries asking for 'last N sites' or 'most recent N sites' (where N is a number), use SearchRecentSharePointSites instead.")]
    public async Task<object> SearchSharePointSites(
        [Description("User access token for SharePoint authentication")] string accessToken,
        [Description("Search query to filter SharePoint sites")] string searchQuery,
        [Description("Maximum number of sites to return (default 10)")] int count = 10,
        [Description("Maximum results limit")] int limit = 10,
        [Description("Additional result constraint")] int maxResults = 10,
        [Description("Response format mode: 'card' for UI display (default), 'analysis' for AI analysis and summaries, 'raw' for unformatted data")] string responseMode = "card")
    {
        try
        {
            _logger.LogInformation("SharePoint search called with query: '{SearchQuery}', count: {Count}, mode: {ResponseMode}", searchQuery, count, responseMode);

            var searchRequest = new SharePointSearchRequest
            {
                Query = searchQuery ?? "",
                MaxResults = Math.Min(count, maxResults),
                SortBy = "modified",
                SortOrder = "desc"
            };

            var searchResponse = await _sharePointService.SearchSharePointSitesAsync(searchRequest, accessToken);

            _logger.LogInformation("SharePoint search completed, found {ResultCount} sites", searchResponse.Sites?.Count ?? 0);

            if (searchResponse.Sites == null || !searchResponse.Sites.Any())
            {
                var emptyResponse = new
                {
                    success = true,
                    message = "No SharePoint sites found matching your search criteria.",
                    sites = new List<object>(),
                    totalCount = 0,
                    searchQuery = searchQuery,
                    responseMode = responseMode,
                    dataType = "sharepoint_sites"
                };

                _logger.LogInformation("Returning empty SharePoint search response");
                return emptyResponse;
            }

            // Format the results based on response mode
            object finalResponse = responseMode.ToLower() switch
            {
                "analysis" => FormatForAnalysisMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                "raw" => FormatForRawMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                _ => FormatForCardMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation)
            };

            _logger.LogInformation("Returning SharePoint search response with {SiteCount} sites in {ResponseMode} mode", searchResponse.Sites.Count, responseMode);
            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SharePoint sites search");
            
            var errorResponse = new
            {
                success = false,
                message = $"Error searching SharePoint sites: {ex.Message}",
                sites = new List<object>(),
                totalCount = 0,
                searchQuery = searchQuery,
                responseMode = responseMode,
                dataType = "sharepoint_sites"
            };

            return errorResponse;
        }
    }

    [McpServerTool]
    [Description("Get the most recent SharePoint sites based on creation or modification date. Use this for queries like 'show me recent SharePoint sites', 'last 5 SharePoint sites', 'newest SharePoint sites', or when user specifically asks for recent sites without a search term.")]
    public async Task<object> SearchRecentSharePointSites(
        [Description("User access token for SharePoint authentication")] string accessToken,
        [Description("Search query filter (optional)")] string searchQuery = "",
        [Description("Number of recent sites to return")] int count = 5,
        [Description("Maximum results limit")] int limit = 5,
        [Description("Additional result constraint")] int maxResults = 5,
        [Description("Response format mode: 'card' for UI display (default), 'analysis' for AI analysis and summaries, 'raw' for unformatted data")] string responseMode = "card")
    {
        try
        {
            _logger.LogInformation("Recent SharePoint search called with query: '{SearchQuery}', count: {Count}, mode: {ResponseMode}", searchQuery, count, responseMode);

            var searchRequest = new SharePointSearchRequest
            {
                Query = searchQuery ?? "",
                MaxResults = Math.Min(count, maxResults),
                SortBy = "modified",
                SortOrder = "desc",
                TimePeriod = "last_month"
            };

            var searchResponse = await _sharePointService.SearchSharePointSitesAsync(searchRequest, accessToken);

            _logger.LogInformation("Recent SharePoint search completed, found {ResultCount} sites", searchResponse.Sites?.Count ?? 0);

            if (searchResponse.Sites == null || !searchResponse.Sites.Any())
            {
                var emptyResponse = new
                {
                    success = true,
                    message = "No recent SharePoint sites found.",
                    sites = new List<object>(),
                    totalCount = 0,
                    searchQuery = searchQuery,
                    responseMode = responseMode,
                    dataType = "sharepoint_sites"
                };

                _logger.LogInformation("Returning empty recent SharePoint search response");
                return emptyResponse;
            }

            // Format the results based on response mode
            object finalResponse = responseMode.ToLower() switch
            {
                "analysis" => FormatForAnalysisMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                "raw" => FormatForRawMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                _ => FormatForCardMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation)
            };

            _logger.LogInformation("Returning recent SharePoint search response with {SiteCount} sites in {ResponseMode} mode", searchResponse.Sites.Count, responseMode);
            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in recent SharePoint sites search");
            
            var errorResponse = new
            {
                success = false,
                message = $"Error searching recent SharePoint sites: {ex.Message}",
                sites = new List<object>(),
                totalCount = 0,
                searchQuery = searchQuery,
                responseMode = responseMode,
                dataType = "sharepoint_sites"
            };

            return errorResponse;
        }
    }

    private object FormatForCardMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        var formattedSites = sites.Select((site, index) => new
        {
            id = $"site_card_{index}",
            title = site.Title ?? "Untitled Site",
            description = site.Description ?? "",
            url = site.Url ?? "",
            created = GetFormattedDate(site.Created, "yyyy-MM-dd"),
            webTemplate = site.WebTemplate ?? "Unknown",
            type = "sharepoint_site",
            status = "active"
        }).ToList();

        return new
        {
            success = true,
            message = $"Found {sites.Count} SharePoint sites" + (!string.IsNullOrEmpty(searchQuery) ? $" matching '{searchQuery}'" : ""),
            sites = formattedSites,
            totalCount = sites.Count,
            searchQuery = searchQuery,
            responseMode = "card",
            dataType = "sharepoint_sites",
            queryExplanation = queryExplanation
        };
    }

    private object FormatForAnalysisMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        var analysisData = sites.Select(site => new
        {
            title = site.Title ?? "Untitled Site",
            url = site.Url ?? "",
            created = GetFormattedDate(site.Created, "yyyy-MM-dd"),
            webTemplate = site.WebTemplate ?? "Unknown",
            purpose = DetermineSitePurpose(site.Title, site.Description),
            activityLevel = CalculateActivityLevel(site.Created, site.LastModified),
            category = CategorizeSite(site.WebTemplate)
        }).ToList();

        return new
        {
            success = true,
            message = $"Analysis of {sites.Count} SharePoint sites" + (!string.IsNullOrEmpty(searchQuery) ? $" matching '{searchQuery}'" : ""),
            sites = analysisData,
            totalCount = sites.Count,
            searchQuery = searchQuery,
            responseMode = "analysis",
            dataType = "sharepoint_sites",
            queryExplanation = queryExplanation,
            summary = new
            {
                totalSites = sites.Count,
                siteTypes = sites.GroupBy(s => s.WebTemplate ?? "Unknown").ToDictionary(g => g.Key, g => g.Count()),
                averageActivityLevel = analysisData.Average(s => s.activityLevel)
            }
        };
    }

    private object FormatForRawMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        return new
        {
            success = true,
            message = $"Raw data for {sites.Count} SharePoint sites" + (!string.IsNullOrEmpty(searchQuery) ? $" matching '{searchQuery}'" : ""),
            sites = sites,
            totalCount = sites.Count,
            searchQuery = searchQuery,
            responseMode = "raw",
            dataType = "sharepoint_sites",
            queryExplanation = queryExplanation
        };
    }

    private string GetFormattedDate(DateTime dateValue, string format)
    {
        return dateValue.ToString(format);
    }

    private string DetermineSitePurpose(string title, string description)
    {
        var content = $"{title} {description}".ToLower();
        
        if (content.Contains("project")) return "Project Management";
        if (content.Contains("team")) return "Team Collaboration";
        if (content.Contains("document")) return "Document Storage";
        if (content.Contains("hr") || content.Contains("human")) return "Human Resources";
        if (content.Contains("marketing")) return "Marketing";
        if (content.Contains("sales")) return "Sales";
        if (content.Contains("finance")) return "Finance";
        
        return "General";
    }

    private double CalculateActivityLevel(DateTime created, DateTime lastModified)
    {
        var daysSinceCreated = (DateTime.Now - created).TotalDays;
        var daysSinceModified = (DateTime.Now - lastModified).TotalDays;
        
        if (daysSinceModified <= 7) return 1.0; // Very active
        if (daysSinceModified <= 30) return 0.8; // Active
        if (daysSinceModified <= 90) return 0.6; // Moderate
        if (daysSinceModified <= 365) return 0.4; // Low
        
        return 0.2; // Very low
    }

    private string CategorizeSite(string webTemplate)
    {
        return webTemplate?.ToUpper() switch
        {
            "STS" => "Team Site",
            "BLOG" => "Blog",
            "WIKI" => "Wiki",
            "BDR" => "Document Center",
            "OFFILE" => "Records Center",
            "TEAMCHANNEL" => "Teams Channel",
            _ => "Standard Site"
        };
    }
}