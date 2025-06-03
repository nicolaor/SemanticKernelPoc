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
        _logger.LogInformation("🟢🟢🟢 SIMPLE TEST METHOD CALLED WITH NO DEPENDENCIES! 🟢🟢🟢");
        _logger.LogInformation("🟢 SimpleTest called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
        _logger.LogInformation("🟢 SimpleTest method executed with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
        return $"🟢 Simple test method executed successfully - accessToken length: {accessToken?.Length ?? 0}";
    }

    [McpServerTool]
    [Description("Search for SharePoint sites using various filters and criteria. Use this when user asks for SharePoint sites, wants to find specific sites, or needs to search by creation date. Ideal for general SharePoint site discovery and when specific search terms or date ranges are provided. For queries asking for 'last N sites' or 'most recent N sites' (where N is a number), use SearchRecentSharePointSites instead.")]
    public async Task<object> SearchSharePointSites(
        [Description("User access token for SharePoint authentication")] string accessToken,
        [Description("Search query to filter SharePoint sites")] string searchQuery,
        [Description("Maximum number of sites to return (default 10)")] int count = 10,
        [Description("Maximum results limit")] int limit = 10,
        [Description("Additional result constraint")] int maxResults = 10)
    {
        try
        {
            _logger.LogInformation("🔥🔥🔥 SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
            _logger.LogInformation("🔥 SearchSharePointSites called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
            _logger.LogInformation("🔥 SearchSharePointSites called with searchQuery: '{SearchQuery}'", searchQuery);
            _logger.LogInformation("🔥 SearchSharePointSites called with count: {Count}", count);

            _logger.LogInformation("🔍 Starting SharePoint sites search with query: '{SearchQuery}', count: {Count}", searchQuery, count);

            // Create SharePointSearchRequest object
            var searchRequest = new SharePointSearchRequest
            {
                Query = searchQuery ?? "",
                MaxResults = Math.Min(count, maxResults),
                SortBy = "modified",
                SortOrder = "desc"
            };

            // Use the actual SharePoint search service
            var searchResponse = await _sharePointService.SearchSharePointSitesAsync(searchRequest, accessToken);

            _logger.LogInformation("✅ SharePoint search completed. Found {ResultCount} sites", searchResponse.Sites?.Count ?? 0);

            // *** LOG COMPLETE SHAREPOINT SEARCH RESPONSE OBJECT ***
            try
            {
                var searchResponseJson = JsonSerializer.Serialize(searchResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📋 === COMPLETE SHAREPOINT SEARCH RESPONSE === {SearchResponseJson}", searchResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize SharePoint search response for logging: {Error}", jsonEx.Message);
            }

            if (searchResponse.Sites == null || !searchResponse.Sites.Any())
            {
                var emptyResponse = new
                {
                    success = true,
                    message = "No SharePoint sites found matching your search criteria.",
                    sites = new List<object>(),
                    totalCount = 0,
                    searchQuery = searchQuery
                };

                // Log empty response
                var emptyResponseJson = JsonSerializer.Serialize(emptyResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("📤 === EMPTY MCP RESPONSE === {EmptyResponseJson}", emptyResponseJson);
                
                return emptyResponse;
            }

            // Format the results for better presentation
            var formattedSites = searchResponse.Sites.Select((site, index) => new
            {
                id = $"site_{index}",
                name = site.Title ?? "Unnamed Site",
                description = site.Description ?? "",
                url = site.Url ?? "",
                created = site.Created.ToString("yyyy-MM-dd") ?? "Unknown",
                lastModified = site.LastModified.ToString("yyyy-MM-dd") ?? "Unknown",
                webTemplate = site.WebTemplate ?? "Unknown"
            }).ToList();

            var finalResponse = new
            {
                success = true,
                message = $"Found {formattedSites.Count} SharePoint sites matching your search.",
                sites = formattedSites,
                totalCount = formattedSites.Count,
                searchQuery = searchQuery,
                queryExplanation = searchResponse.QueryExplanation
            };

            // *** LOG COMPLETE FINAL MCP RESPONSE ***
            try
            {
                var finalResponseJson = JsonSerializer.Serialize(finalResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📤 === FINAL MCP RESPONSE TO CLIENT === {FinalResponseJson}", finalResponseJson);
                
                // Also log individual site details for debugging
                _logger.LogInformation("🔍 === INDIVIDUAL SITE DETAILS ===");
                for (int i = 0; i < formattedSites.Count; i++)
                {
                    var site = formattedSites[i];
                    _logger.LogInformation("   Site {Index}: Name='{Name}', URL='{Url}', Created='{Created}'", 
                        i + 1, site.name, site.url, site.created);
                }
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize final MCP response for logging: {Error}", jsonEx.Message);
            }

            _logger.LogInformation("🚀 RETURNING SUCCESS RESPONSE WITH {SiteCount} SITES", formattedSites.Count);
            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in SharePoint sites search");
            
            var errorResponse = new
            {
                success = false,
                message = $"Error searching SharePoint sites: {ex.Message}",
                sites = new List<object>(),
                totalCount = 0,
                searchQuery = searchQuery
            };

            // Log error response
            try
            {
                var errorResponseJson = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogError("📤 === ERROR MCP RESPONSE === {ErrorResponseJson}", errorResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize error response for logging: {Error}", jsonEx.Message);
            }

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
        [Description("Additional result constraint")] int maxResults = 5)
    {
        try
        {
            _logger.LogInformation("🔥🔥🔥 RECENT SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with searchQuery: '{SearchQuery}'", searchQuery);
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with count: {Count}", count);

            _logger.LogInformation("🔍 Starting recent SharePoint sites search with count: {Count}", count);

            // Create SharePointSearchRequest object for recent sites (sorted by modified date descending)
            var searchRequest = new SharePointSearchRequest
            {
                Query = searchQuery ?? "",
                MaxResults = Math.Min(count, maxResults),
                SortBy = "modified",
                SortOrder = "desc",
                TimePeriod = "last_month" // Focus on recent sites from last month
            };

            // Use the actual SharePoint search service for recent sites
            var searchResponse = await _sharePointService.SearchSharePointSitesAsync(searchRequest, accessToken);

            _logger.LogInformation("✅ Recent SharePoint sites search completed. Found {ResultCount} sites", searchResponse.Sites?.Count ?? 0);

            // *** LOG COMPLETE SHAREPOINT SEARCH RESPONSE OBJECT FOR RECENT SITES ***
            try
            {
                var searchResponseJson = JsonSerializer.Serialize(searchResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📋 === COMPLETE RECENT SITES SHAREPOINT RESPONSE === {SearchResponseJson}", searchResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize recent sites SharePoint search response for logging: {Error}", jsonEx.Message);
            }

            if (searchResponse.Sites == null || !searchResponse.Sites.Any())
            {
                var emptyResponse = new
                {
                    success = true,
                    message = "No recent SharePoint sites found.",
                    sites = new List<object>(),
                    totalCount = 0,
                    requestedCount = count
                };

                // Log empty response
                var emptyResponseJson = JsonSerializer.Serialize(emptyResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("📤 === EMPTY RECENT SITES MCP RESPONSE === {EmptyResponseJson}", emptyResponseJson);
                
                return emptyResponse;
            }

            // Format the results
            var formattedSites = searchResponse.Sites.Select((site, index) => new
            {
                id = $"recent_site_{index}",
                name = site.Title ?? "Unnamed Site",
                description = site.Description ?? "",
                url = site.Url ?? "",
                created = site.Created.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                lastModified = site.LastModified.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                webTemplate = site.WebTemplate ?? "Unknown"
            }).ToList();

            var finalResponse = new
            {
                success = true,
                message = $"Found {formattedSites.Count} recent SharePoint sites.",
                sites = formattedSites,
                totalCount = formattedSites.Count,
                requestedCount = count,
                queryExplanation = searchResponse.QueryExplanation
            };

            // *** LOG COMPLETE FINAL MCP RESPONSE FOR RECENT SITES ***
            try
            {
                var finalResponseJson = JsonSerializer.Serialize(finalResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📤 === FINAL RECENT SITES MCP RESPONSE TO CLIENT === {FinalResponseJson}", finalResponseJson);
                
                // Also log individual site details for debugging
                _logger.LogInformation("🔍 === INDIVIDUAL RECENT SITE DETAILS ===");
                for (int i = 0; i < formattedSites.Count; i++)
                {
                    var site = formattedSites[i];
                    _logger.LogInformation("   Recent Site {Index}: Name='{Name}', URL='{Url}', LastModified='{LastModified}'", 
                        i + 1, site.name, site.url, site.lastModified);
                }
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize final recent sites MCP response for logging: {Error}", jsonEx.Message);
            }

            _logger.LogInformation("🚀 RETURNING RECENT SITES SUCCESS RESPONSE WITH {SiteCount} SITES", formattedSites.Count);
            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in recent SharePoint sites search");
            
            var errorResponse = new
            {
                success = false,
                message = $"Error getting recent SharePoint sites: {ex.Message}",
                sites = new List<object>(),
                totalCount = 0,
                requestedCount = count
            };

            // Log error response
            try
            {
                var errorResponseJson = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogError("📤 === ERROR RECENT SITES MCP RESPONSE === {ErrorResponseJson}", errorResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize error response for logging: {Error}", jsonEx.Message);
            }

            return errorResponse;
        }
    }
}