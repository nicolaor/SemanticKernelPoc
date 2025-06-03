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
        [Description("Additional result constraint")] int maxResults = 10,
        [Description("Response format mode: 'card' for UI display (default), 'analysis' for AI analysis and summaries, 'raw' for unformatted data")] string responseMode = "card")
    {
        try
        {
            _logger.LogInformation("🔥🔥🔥 SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
            _logger.LogInformation("🔥 SearchSharePointSites called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
            _logger.LogInformation("🔥 SearchSharePointSites called with searchQuery: '{SearchQuery}'", searchQuery);
            _logger.LogInformation("🔥 SearchSharePointSites called with count: {Count}", count);
            _logger.LogInformation("🔥 SearchSharePointSites called with responseMode: '{ResponseMode}'", responseMode);

            _logger.LogInformation("🔍 Starting SharePoint sites search with query: '{SearchQuery}', count: {Count}, mode: {ResponseMode}", searchQuery, count, responseMode);

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
                    searchQuery = searchQuery,
                    responseMode = responseMode,
                    dataType = "sharepoint_sites"
                };

                // Log empty response
                var emptyResponseJson = JsonSerializer.Serialize(emptyResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("📤 === EMPTY MCP RESPONSE === {EmptyResponseJson}", emptyResponseJson);
                
                return emptyResponse;
            }

            // Format the results based on response mode
            object finalResponse = responseMode.ToLower() switch
            {
                "analysis" => FormatForAnalysisMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                "raw" => FormatForRawMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                _ => FormatForCardMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation) // default to card mode
            };

            // *** LOG COMPLETE FINAL MCP RESPONSE ***
            try
            {
                var finalResponseJson = JsonSerializer.Serialize(finalResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📤 === FINAL MCP RESPONSE TO CLIENT (Mode: {ResponseMode}) === {FinalResponseJson}", responseMode, finalResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize final MCP response for logging: {Error}", jsonEx.Message);
            }

            _logger.LogInformation("🚀 RETURNING SUCCESS RESPONSE WITH {SiteCount} SITES IN {ResponseMode} MODE", searchResponse.Sites.Count, responseMode);
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
                searchQuery = searchQuery,
                responseMode = responseMode,
                dataType = "sharepoint_sites"
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
        [Description("Additional result constraint")] int maxResults = 5,
        [Description("Response format mode: 'card' for UI display (default), 'analysis' for AI analysis and summaries, 'raw' for unformatted data")] string responseMode = "card")
    {
        try
        {
            _logger.LogInformation("🔥🔥🔥 RECENT SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with accessToken length: {TokenLength}", accessToken?.Length ?? 0);
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with searchQuery: '{SearchQuery}'", searchQuery);
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with count: {Count}", count);
            _logger.LogInformation("🔥 SearchRecentSharePointSites called with responseMode: '{ResponseMode}'", responseMode);

            _logger.LogInformation("🔍 Starting recent SharePoint sites search with count: {Count}, mode: {ResponseMode}", count, responseMode);

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
                    requestedCount = count,
                    responseMode = responseMode,
                    dataType = "sharepoint_sites"
                };

                // Log empty response
                var emptyResponseJson = JsonSerializer.Serialize(emptyResponse, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("📤 === EMPTY RECENT SITES MCP RESPONSE === {EmptyResponseJson}", emptyResponseJson);
                
                return emptyResponse;
            }

            // Format the results based on response mode
            object finalResponse = responseMode.ToLower() switch
            {
                "analysis" => FormatForAnalysisMode(searchResponse.Sites, $"recent sites {(!string.IsNullOrEmpty(searchQuery) ? $"matching '{searchQuery}'" : "")}", searchResponse.QueryExplanation),
                "raw" => FormatForRawMode(searchResponse.Sites, searchQuery, searchResponse.QueryExplanation),
                _ => FormatForCardMode(searchResponse.Sites, $"recent sites {(!string.IsNullOrEmpty(searchQuery) ? $"matching '{searchQuery}'" : "")}", searchResponse.QueryExplanation) // default to card mode
            };

            // *** LOG COMPLETE FINAL MCP RESPONSE FOR RECENT SITES ***
            try
            {
                var finalResponseJson = JsonSerializer.Serialize(finalResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("📤 === FINAL RECENT SITES MCP RESPONSE TO CLIENT (Mode: {ResponseMode}) === {FinalResponseJson}", responseMode, finalResponseJson);
            }
            catch (Exception jsonEx)
            {
                _logger.LogWarning("⚠️ Failed to serialize final recent sites MCP response for logging: {Error}", jsonEx.Message);
            }

            _logger.LogInformation("🚀 RETURNING RECENT SITES SUCCESS RESPONSE WITH {SiteCount} SITES IN {ResponseMode} MODE", searchResponse.Sites.Count, responseMode);
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
                requestedCount = count,
                responseMode = responseMode,
                dataType = "sharepoint_sites"
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

    // *** NEW HELPER METHODS FOR DIFFERENT RESPONSE FORMATS ***

    private object FormatForCardMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        _logger.LogInformation("🃏 Formatting response for CARD mode");
        
        var formattedSites = sites.Select((site, index) =>
        {
            return new
            {
                id = $"site_card_{index}",
                title = site.Title ?? "Unnamed Site",
                description = site.Description ?? "",
                url = site.Url ?? "",
                created = GetFormattedDate(site.Created.ToString(), "yyyy-MM-dd"),
                createdFormatted = GetFormattedDate(site.Created.ToString(), "MMM dd, yyyy"),
                lastModified = GetFormattedDate(site.LastModified.ToString(), "yyyy-MM-dd"),
                lastModifiedFormatted = GetFormattedDate(site.LastModified.ToString(), "MMM dd, yyyy"),
                webTemplate = site.WebTemplate ?? "Unknown",
                type = "sharepoint_site",
                status = "active"
            };
        }).ToList();

        return new
        {
            success = true,
            message = $"Found {formattedSites.Count} SharePoint sites.",
            sites = formattedSites,
            totalCount = formattedSites.Count,
            searchQuery = searchQuery,
            queryExplanation = queryExplanation,
            responseMode = "card",
            dataType = "sharepoint_sites",
            // Add metadata for card rendering
            displayFormat = "cards",
            cardType = "sharepoint_site"
        };
    }

    private object FormatForAnalysisMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        _logger.LogInformation("🔍 Formatting response for ANALYSIS mode");
        
        var analysisData = sites.Select((site, index) =>
        {
            return new
            {
                title = site.Title ?? "Unnamed Site",
                description = site.Description ?? "",
                url = site.Url ?? "",
                created = GetFormattedDate(site.Created.ToString(), "yyyy-MM-dd HH:mm"),
                lastModified = GetFormattedDate(site.LastModified.ToString(), "yyyy-MM-dd HH:mm"),
                webTemplate = site.WebTemplate ?? "Unknown",
                purpose = DetermineSitePurpose(site.Title, site.Description),
                activity = CalculateActivityLevel(site.Created.ToString(), site.LastModified.ToString()),
                category = CategorizeSite(site.WebTemplate)
            };
        }).ToList();

        return new
        {
            success = true,
            message = $"Analysis data for {analysisData.Count} SharePoint sites.",
            sites = analysisData,
            totalCount = analysisData.Count,
            searchQuery = searchQuery,
            queryExplanation = queryExplanation,
            responseMode = "analysis",
            dataType = "sharepoint_sites",
            // Add analysis metadata
            analysisContext = new
            {
                totalSites = analysisData.Count,
                averageActivity = analysisData.Any() ? analysisData.Average(s => s.activity) : 0,
                templates = analysisData.GroupBy(s => s.webTemplate).Select(g => new { template = g.Key, count = g.Count() }),
                categories = analysisData.GroupBy(s => s.category).Select(g => new { category = g.Key, count = g.Count() })
            }
        };
    }

    private object FormatForRawMode(List<SharePointSite> sites, string searchQuery, string queryExplanation)
    {
        _logger.LogInformation("🗃️ Formatting response for RAW mode");
        
        return new
        {
            success = true,
            message = $"Raw data for {sites.Count} SharePoint sites.",
            sites = sites, // Return unformatted site data
            totalCount = sites.Count,
            searchQuery = searchQuery,
            queryExplanation = queryExplanation,
            responseMode = "raw",
            dataType = "sharepoint_sites"
        };
    }

    // *** HELPER METHODS FOR DATA EXTRACTION AND ANALYSIS ***

    private string GetFormattedDate(string dateValue, string format)
    {
        if (string.IsNullOrEmpty(dateValue)) return "Unknown";
        
        if (DateTime.TryParse(dateValue, out var date))
        {
            return date.ToString(format);
        }
        
        return dateValue;
    }

    private string DetermineSitePurpose(string title, string description)
    {
        var content = $"{title} {description}".ToLower();
        
        if (content.Contains("team") || content.Contains("collaboration")) return "Team Collaboration";
        if (content.Contains("project")) return "Project Management";
        if (content.Contains("document") || content.Contains("file")) return "Document Repository";
        if (content.Contains("communication") || content.Contains("news")) return "Communication Hub";
        if (content.Contains("hr") || content.Contains("human")) return "Human Resources";
        if (content.Contains("finance") || content.Contains("budget")) return "Finance";
        if (content.Contains("marketing")) return "Marketing";
        if (content.Contains("sales")) return "Sales";
        
        return "General Purpose";
    }

    private double CalculateActivityLevel(string created, string lastModified)
    {
        if (!DateTime.TryParse(created, out var createdDate) || 
            !DateTime.TryParse(lastModified, out var modifiedDate))
        {
            return 0.5; // Default medium activity
        }
        
        var totalDays = (DateTime.Now - createdDate).TotalDays;
        var daysSinceModified = (DateTime.Now - modifiedDate).TotalDays;
        
        if (totalDays == 0) return 1.0;
        
        // Activity score: 1.0 = very active, 0.0 = inactive
        var activityScore = Math.Max(0.0, 1.0 - (daysSinceModified / Math.Max(totalDays, 30)));
        return Math.Round(activityScore, 2);
    }

    private string CategorizeSite(string webTemplate)
    {
        return webTemplate?.ToLower() switch
        {
            var t when t.Contains("team") => "Team Site",
            var t when t.Contains("communication") => "Communication Site",
            var t when t.Contains("hub") => "Hub Site",
            var t when t.Contains("group") => "Group Site",
            var t when t.Contains("project") => "Project Site",
            var t when t.Contains("blog") => "Blog",
            var t when t.Contains("wiki") => "Wiki",
            _ => "Standard Site"
        };
    }
}