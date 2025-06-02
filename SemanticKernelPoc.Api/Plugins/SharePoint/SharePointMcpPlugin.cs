using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using SemanticKernelPoc.Api.Services;
using System.Text.Json;

namespace SemanticKernelPoc.Api.Plugins.SharePoint;

public class SharePointMcpPlugin
{
    private readonly IMcpClientService _mcpClientService;
    private readonly ILogger<SharePointMcpPlugin> _logger;

    public SharePointMcpPlugin(IMcpClientService mcpClientService, ILogger<SharePointMcpPlugin> logger)
    {
        _mcpClientService = mcpClientService;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Search for SharePoint sites using various filters and criteria. Use this when user asks for SharePoint sites, wants to find specific sites, or needs to search by creation date. Ideal for general SharePoint site discovery and when specific search terms or date ranges are provided. For queries asking for 'last N sites' or 'most recent N sites' (where N is a number), use this function with maxResults=N and no time filters to get the N most recently created sites.")]
    public async Task<string> search_sharepoint_sites(
        Kernel kernel,
        [Description("Text search query to filter sites by title or description - use keywords from user's request")] string query = null,
        [Description("Filter for sites created after this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ) - extract from user phrases like 'since January' or 'after last week'")] string createdAfter = null,
        [Description("Filter for sites created before this date (ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ) - extract from user phrases like 'before March' or 'until yesterday'")] string createdBefore = null,
        [Description("Maximum number of results to return (default: 50, max: 500). IMPORTANT: When user asks for 'last N sites' or 'most recent N sites', set this to N to get the N most recently created sites ordered by creation date.")] int maxResults = 50,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
    {
        try
        {
            // Step 1: Validate authentication
            var userToken = await GetUserTokenFromKernel(kernel);
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User authentication token not available.";
            }

            // Step 2: Log and execute search
            LogSearchRequest(query, createdAfter, createdBefore, maxResults, analysisMode);
            var sites = await ExecuteSharePointSearch(userToken, query, createdAfter, createdBefore, maxResults);

            // Step 3: Generate response based on mode
            return analysisMode 
                ? GenerateAnalysisResponse(kernel, sites, query)
                : GenerateCardResponse(kernel, sites, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SharePoint sites");
            return $"Error searching SharePoint sites: {ex.Message}";
        }
    }

    #region SharePoint Search Helper Methods

    private void LogSearchRequest(string query, string createdAfter, string createdBefore, int maxResults, bool analysisMode)
    {
        _logger.LogInformation("Searching SharePoint sites with query: {Query}, createdAfter: {CreatedAfter}, createdBefore: {CreatedBefore}, maxResults: {MaxResults}, analysisMode: {AnalysisMode}",
            query, createdAfter, createdBefore, maxResults, analysisMode);
    }

    private async Task<List<SharePointSiteData>> ExecuteSharePointSearch(string userToken, string query, string createdAfter, string createdBefore, int maxResults)
    {
        var parameters = BuildSearchParameters(userToken, query, createdAfter, createdBefore, maxResults);
        var result = await _mcpClientService.CallMcpFunctionAsync("SearchSharePointSites", parameters);
        
        _logger.LogInformation("SharePoint sites search completed successfully. Result: {Result}", result);
        
        return ParseMcpResult(result);
    }

    private Dictionary<string, object> BuildSearchParameters(string userToken, string query, string createdAfter, string createdBefore, int maxResults)
    {
        return new Dictionary<string, object>
        {
            ["userToken"] = userToken,
            ["query"] = query ?? string.Empty,
            ["createdAfter"] = createdAfter ?? string.Empty,
            ["createdBefore"] = createdBefore ?? string.Empty,
            ["maxResults"] = maxResults
        };
    }

    private string GenerateAnalysisResponse(Kernel kernel, List<SharePointSiteData> sites, string query)
    {
        var userName = GetUserNameFromKernel(kernel);
        var siteBreakdown = AnalyzeSiteBreakdown(sites);
        var dateRange = GetSiteDateRange(sites);
        
        return $"Found {sites.Count} SharePoint sites for {userName}. " +
               $"Breakdown: {siteBreakdown.TeamSites} Team sites, {siteBreakdown.ClassicSites} Classic sites, " +
               $"{siteBreakdown.OtherSites} other types. " +
               (!string.IsNullOrEmpty(query) ? $"Search query: '{query}'. " : "") +
               $"Sites range from {dateRange.MinDate:yyyy-MM-dd} to {dateRange.MaxDate:yyyy-MM-dd}.";
    }

    private string GenerateCardResponse(Kernel kernel, List<SharePointSiteData> sites, string query)
    {
        var sharePointCards = BuildSharePointCards(sites);
        SetKernelCardData(kernel, sharePointCards);
        
        var responseMessage = BuildCardResponseMessage(sharePointCards.Count, query);
        kernel.Data["SharePointFunctionResponse"] = responseMessage;
        
        return responseMessage;
    }

    private string GetUserNameFromKernel(Kernel kernel)
    {
        return kernel.Data.TryGetValue("UserName", out object userNameObj) ? userNameObj?.ToString() : "User";
    }

    private (int TeamSites, int ClassicSites, int OtherSites) AnalyzeSiteBreakdown(List<SharePointSiteData> sites)
    {
        var teamSiteCount = sites.Count(s => s.WebTemplate.ToLower() == "group");
        var classSiteCount = sites.Count(s => s.WebTemplate.ToLower() == "sts");
        var otherSiteCount = sites.Count - teamSiteCount - classSiteCount;
        
        return (teamSiteCount, classSiteCount, otherSiteCount);
    }

    private (DateTime MinDate, DateTime MaxDate) GetSiteDateRange(List<SharePointSiteData> sites)
    {
        return sites.Any() 
            ? (sites.Min(s => s.Created), sites.Max(s => s.Created))
            : (DateTime.MinValue, DateTime.MinValue);
    }

    private List<object> BuildSharePointCards(List<SharePointSiteData> sites)
    {
        return sites.Select(site => new
        {
            title = site.Title,
            url = site.Url,
            created = site.Created.ToString("yyyy-MM-dd"),
            webTemplate = site.WebTemplate,
            description = site.Description
        }).Cast<object>().ToList();
    }

    private void SetKernelCardData(Kernel kernel, List<object> sharePointCards)
    {
        kernel.Data["SharePointCards"] = sharePointCards;
        kernel.Data["HasStructuredData"] = "true";
        kernel.Data["StructuredDataType"] = "sharepoint";
        kernel.Data["StructuredDataCount"] = sharePointCards.Count;
    }

    private string BuildCardResponseMessage(int cardCount, string query)
    {
        return $"I found {cardCount} SharePoint sites" + 
               (!string.IsNullOrEmpty(query) ? $" matching '{query}'" : "") + 
               ". The details are displayed in the cards below.";
    }

    #endregion

    [KernelFunction]
    [Description("Search for recently created SharePoint sites within a specific time period (created within the last specified number of days). Use this ONLY when user specifically mentions time periods like 'last 30 days', 'this month', 'past week', 'sites created in the last 7 days'. Do NOT use this for queries like 'last 3 sites' or 'most recent 5 sites' - those should use search_sharepoint_sites with maxResults instead.")]
    public async Task<string> search_recent_sharepoint_sites(
        Kernel kernel,
        [Description("Text search query to filter recent sites - use keywords from user's request")] string query = null,
        [Description("Number of days to look back (default: 30) - interpret from user phrases like 'last week' (7), 'this month' (30), 'past 3 days' (3). Only use this when user specifically mentions a time period, not when they ask for 'last N sites'.")] int daysBack = 30,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
    {
        try
        {
            // Step 1: Validate authentication
            var userToken = await GetUserTokenFromKernel(kernel);
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User authentication token not available.";
            }

            // Step 2: Execute recent sites search
            LogRecentSearchRequest(daysBack, analysisMode);
            var sites = await ExecuteRecentSharePointSearch(userToken, query, daysBack);

            // Step 3: Generate response based on mode
            return analysisMode 
                ? GenerateRecentAnalysisResponse(kernel, sites, query, daysBack)
                : GenerateRecentCardResponse(kernel, sites, query, daysBack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent SharePoint sites");
            return $"Error searching recent SharePoint sites: {ex.Message}";
        }
    }

    #region Recent SharePoint Sites Helper Methods

    private void LogRecentSearchRequest(int daysBack, bool analysisMode)
    {
        _logger.LogInformation("Searching for recent SharePoint sites created in the last {DaysBack} days, analysisMode: {AnalysisMode}", daysBack, analysisMode);
    }

    private async Task<List<SharePointSiteData>> ExecuteRecentSharePointSearch(string userToken, string query, int daysBack)
    {
        var parameters = BuildRecentSearchParameters(userToken, query, daysBack);
        var result = await _mcpClientService.CallMcpFunctionAsync("SearchRecentSharePointSites", parameters);
        
        _logger.LogInformation("Recent SharePoint sites search completed successfully. Result: {Result}", result);
        
        return ParseMcpResult(result);
    }

    private Dictionary<string, object> BuildRecentSearchParameters(string userToken, string query, int daysBack)
    {
        return new Dictionary<string, object>
        {
            ["userToken"] = userToken,
            ["query"] = query ?? string.Empty,
            ["daysBack"] = daysBack
        };
    }

    private string GenerateRecentAnalysisResponse(Kernel kernel, List<SharePointSiteData> sites, string query, int daysBack)
    {
        var userName = GetUserNameFromKernel(kernel);
        var timePeriod = FormatTimePeriod(daysBack);
        var siteTypesSummary = BuildSiteTypesSummary(sites);
        var mostRecentInfo = GetMostRecentSiteInfo(sites);
        
        return $"Found {sites.Count} SharePoint sites created in the {timePeriod} for {userName}. " +
               $"Site types: {siteTypesSummary}. " +
               (!string.IsNullOrEmpty(query) ? $"Filtered by: '{query}'. " : "") +
               mostRecentInfo;
    }

    private string GenerateRecentCardResponse(Kernel kernel, List<SharePointSiteData> sites, string query, int daysBack)
    {
        var sharePointCards = BuildSharePointCards(sites);
        SetKernelCardData(kernel, sharePointCards);
        
        var timePeriod = FormatTimePeriod(daysBack);
        var responseMessage = BuildRecentCardResponseMessage(sharePointCards.Count, timePeriod, query);
        kernel.Data["SharePointFunctionResponse"] = responseMessage;
        
        return responseMessage;
    }

    private string FormatTimePeriod(int daysBack)
    {
        return daysBack == 7 ? "past week" : daysBack == 30 ? "past month" : $"past {daysBack} days";
    }

    private string BuildSiteTypesSummary(List<SharePointSiteData> sites)
    {
        return string.Join(", ", sites.GroupBy(s => s.WebTemplate).Select(g => $"{g.Count()} {g.Key}"));
    }

    private string GetMostRecentSiteInfo(List<SharePointSiteData> sites)
    {
        return sites.Any() 
            ? $"Most recent: {sites.OrderByDescending(s => s.Created).First().Title} ({sites.Max(s => s.Created):yyyy-MM-dd})."
            : "";
    }

    private string BuildRecentCardResponseMessage(int cardCount, string timePeriod, string query)
    {
        return $"I found {cardCount} SharePoint sites created in the {timePeriod}" + 
               (!string.IsNullOrEmpty(query) ? $" matching '{query}'" : "") + 
               ". The details are displayed in the cards below.";
    }

    #endregion

    [KernelFunction]
    [Description("Find SharePoint sites that match specific keywords in their title or description. Use this when user provides specific keywords, names, or terms to search for. Best for targeted searches when user knows what they're looking for.")]
    public async Task<string> find_sharepoint_sites_by_keyword(
        Kernel kernel,
        [Description("Keywords to search for in site titles and descriptions - extract key terms from user's question")] string keywords,
        [Description("Maximum number of results to return (default: 20, max: 500)")] int maxResults = 20,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
    {
        try
        {
            var userToken = await GetUserTokenFromKernel(kernel);
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User authentication token not available.";
            }

            if (string.IsNullOrWhiteSpace(keywords))
            {
                return "Please provide keywords to search for.";
            }

            _logger.LogInformation("Searching SharePoint sites by keywords: {Keywords}, analysisMode: {AnalysisMode}", keywords, analysisMode);

            var parameters = new Dictionary<string, object>
            {
                ["userToken"] = userToken,
                ["keywords"] = keywords,
                ["maxResults"] = maxResults
            };

            var result = await _mcpClientService.CallMcpFunctionAsync("FindSharePointSitesByKeyword", parameters);

            _logger.LogInformation("SharePoint sites keyword search completed successfully. Result: {Result}", result);
            
            // Parse the MCP result to extract sites data
            var sites = ParseMcpResult(result);
            
            if (analysisMode)
            {
                // For analysis mode, don't store structured data - return text only
                var userName = kernel.Data.TryGetValue("UserName", out object userNameObj) ? userNameObj?.ToString() : "User";
                var titleMatches = sites.Count(s => s.Title.ToLower().Contains(keywords.ToLower()));
                var descMatches = sites.Count(s => !string.IsNullOrEmpty(s.Description) && s.Description.ToLower().Contains(keywords.ToLower()));
                
                return $"Found {sites.Count} SharePoint sites matching keywords '{keywords}' for {userName}. " +
                       $"Match breakdown: {titleMatches} in titles, {descMatches} in descriptions. " +
                       $"Site types: {string.Join(", ", sites.GroupBy(s => s.WebTemplate).Select(g => $"{g.Count()} {g.Key}"))}. " +
                       (sites.Any() ? $"Most relevant: {sites.First().Title}." : "");
            }
            else
            {
                // For card display mode, create structured data for cards
                var sharePointCards = sites.Select(site => new
                {
                    title = site.Title,
                    url = site.Url,
                    created = site.Created.ToString("yyyy-MM-dd"),
                    webTemplate = site.WebTemplate,
                    description = site.Description
                }).ToList();

                kernel.Data["SharePointCards"] = sharePointCards;
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "sharepoint";
                kernel.Data["StructuredDataCount"] = sharePointCards.Count;

                var responseMessage = $"I found {sharePointCards.Count} SharePoint sites matching '{keywords}'. The details are displayed in the cards below.";
                
                // Store the function response for the controller to use
                kernel.Data["SharePointFunctionResponse"] = responseMessage;
                
                return responseMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SharePoint sites by keyword");
            return $"Error searching SharePoint sites by keyword: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Advanced search for SharePoint sites with intelligent parsing and complex filtering options. Use this for complex queries involving multiple criteria, sorting requirements, or when user asks for advanced search features. Examples: 'find sites sorted by creation date', 'search for exact phrase matches', or queries with multiple filters. IMPORTANT: When user asks for 'last N sites' or 'most recent N sites', use search_sharepoint_sites instead with maxResults=N.")]
    public async Task<string> search_sharepoint_sites_advanced(
        Kernel kernel,
        [Description("Natural language search query that will be intelligently parsed - include full user request for smart interpretation")] string query = null,
        [Description("Specific time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', 'this_quarter', 'last_quarter', 'this_year', 'last_year', or patterns like 'last_7_days'. Do NOT use for 'last N sites' queries.")] string timePeriod = null,
        [Description("List of specific keywords to search for (comma-separated)")] string keywords = null,
        [Description("Search scope: 'title', 'description', 'title_and_description', or 'all' (default)")] string searchScope = "all",
        [Description("Sort by: 'relevance', 'created', 'modified', or 'title'")] string sortBy = "relevance",
        [Description("Sort order: 'desc' or 'asc'")] string sortOrder = "desc",
        [Description("Whether to use exact phrase matching")] bool exactMatch = false,
        [Description("Maximum number of results to return (default: 20, max: 500). When user asks for 'last N sites', set this to N and sortBy to 'created' with sortOrder 'desc'.")] int maxResults = 20,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
    {
        try
        {
            var userToken = await GetUserTokenFromKernel(kernel);
            if (string.IsNullOrEmpty(userToken))
            {
                return "Error: User authentication token not available.";
            }

            _logger.LogInformation("Advanced SharePoint search - Query: {Query}, TimePeriod: {TimePeriod}, Keywords: {Keywords}, Scope: {SearchScope}, Sort: {SortBy} {SortOrder}, AnalysisMode: {AnalysisMode}",
                query, timePeriod, keywords, searchScope, sortBy, sortOrder, analysisMode);

            // Parse keywords string into list
            var keywordsList = !string.IsNullOrEmpty(keywords) 
                ? keywords.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList()
                : new List<string>();

            var parameters = new Dictionary<string, object>
            {
                ["userToken"] = userToken,
                ["query"] = query ?? string.Empty,
                ["timePeriod"] = timePeriod ?? string.Empty,
                ["keywords"] = keywordsList,
                ["searchScope"] = searchScope ?? "all",
                ["sortBy"] = sortBy ?? "relevance",
                ["sortOrder"] = sortOrder ?? "desc",
                ["exactMatch"] = exactMatch,
                ["maxResults"] = maxResults
            };

            var result = await _mcpClientService.CallMcpFunctionAsync("SearchSharePointSitesAdvanced", parameters);

            _logger.LogInformation("Advanced SharePoint search completed successfully. Result: {Result}", result);
            
            // Parse the MCP result to extract sites data
            var sites = ParseMcpResult(result);
            
            if (analysisMode)
            {
                // For analysis mode, don't store structured data - return text only
                var userName = kernel.Data.TryGetValue("UserName", out object userNameObj) ? userNameObj?.ToString() : "User";
                var searchDetails = BuildSearchSummary(query, timePeriod, keywordsList, searchScope, sortBy, exactMatch);
                
                return $"Advanced search found {sites.Count} SharePoint sites for {userName}. " +
                       $"Search criteria: {searchDetails}. " +
                       $"Results sorted by {sortBy} ({sortOrder}). " +
                       $"Site distribution: {string.Join(", ", sites.GroupBy(s => s.WebTemplate).Select(g => $"{g.Count()} {g.Key}"))}." +
                       (sites.Any() ? $" Date range: {sites.Min(s => s.Created):yyyy-MM-dd} to {sites.Max(s => s.Created):yyyy-MM-dd}." : "");
            }
            else
            {
                // For card display mode, create structured data for cards
                var sharePointCards = sites.Select(site => new
                {
                    title = site.Title,
                    url = site.Url,
                    created = site.Created.ToString("yyyy-MM-dd"),
                    webTemplate = site.WebTemplate,
                    description = site.Description
                }).ToList();

                kernel.Data["SharePointCards"] = sharePointCards;
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "sharepoint";
                kernel.Data["StructuredDataCount"] = sharePointCards.Count;

                var responseMessage = $"I found {sharePointCards.Count} SharePoint sites" +
                       (!string.IsNullOrEmpty(query) ? $" for '{query}'" : "") +
                       (!string.IsNullOrEmpty(timePeriod) ? $" from {timePeriod}" : "") +
                       $", sorted by {sortBy}. The details are displayed in the cards below.";
                
                // Store the function response for the controller to use
                kernel.Data["SharePointFunctionResponse"] = responseMessage;
                
                return responseMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced SharePoint search");
            return $"Error in advanced search: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Check the status and connectivity of the MCP server and SharePoint services. Use this when user asks about server status, connectivity issues, or wants to verify SharePoint access. Also use to troubleshoot authentication problems.")]
    public async Task<string> check_mcp_server_status(
        Kernel kernel,
        [Description("Whether to include full SharePoint connectivity test (default true)")] bool includeSharePointTest = true)
    {
        try
        {
            string userToken = null;
            
            if (includeSharePointTest)
            {
                userToken = await GetUserTokenFromKernel(kernel);
                if (string.IsNullOrEmpty(userToken))
                {
                    return "⚠️ MCP server is running, but user authentication token is not available for SharePoint connectivity test.";
                }
            }

            var parameters = new Dictionary<string, object>
            {
                ["includeSharePointTest"] = includeSharePointTest,
                ["userToken"] = userToken ?? string.Empty
            };

            var result = await _mcpClientService.CallMcpFunctionAsync("CheckMcpServerStatus", parameters);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MCP server status");
            return $"Error checking server status: {ex.Message}";
        }
    }

    private Task<string> GetUserTokenFromKernel(Kernel kernel)
    {
        try
        {
            var token = kernel.Data.TryGetValue("UserAccessToken", out object tokenObj) ? tokenObj?.ToString() : null;
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("UserAccessToken not found in kernel data");
            }
            return Task.FromResult(token ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user token from kernel");
            return Task.FromResult(string.Empty);
        }
    }

    private List<SharePointSiteData> ParseMcpResult(string mcpResult)
    {
        try
        {
            // Check for error messages first
            if (mcpResult.StartsWith("Error") || mcpResult.Contains("MsalUiRequiredException") || mcpResult.Contains("authentication") || mcpResult.Contains("unauthorized"))
            {
                _logger.LogWarning("MCP server returned error: {Error}", mcpResult);
                return new List<SharePointSiteData>();
            }
            
            // Handle MCP content format: [{"type":"text","text":"[{...}]"}]
            if (mcpResult.StartsWith("[") && mcpResult.Contains("\"type\":\"text\""))
            {
                try
                {
                    // Parse as MCP content array
                    var mcpContent = JsonSerializer.Deserialize<JsonElement[]>(mcpResult);
                    if (mcpContent != null && mcpContent.Length > 0)
                    {
                        var firstContent = mcpContent[0];
                        if (firstContent.TryGetProperty("type", out var typeProperty) && 
                            typeProperty.GetString() == "text" &&
                            firstContent.TryGetProperty("text", out var textProperty))
                        {
                            var jsonData = textProperty.GetString();
                            if (!string.IsNullOrEmpty(jsonData))
                            {
                                var sites = JsonSerializer.Deserialize<List<SharePointSiteData>>(jsonData, new JsonSerializerOptions 
                                { 
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    PropertyNameCaseInsensitive = true
                                });
                                
                                // Filter out any sites with empty URLs
                                var validSites = sites?.Where(s => !string.IsNullOrEmpty(s.Url) && !string.IsNullOrEmpty(s.Title)).ToList() ?? new List<SharePointSiteData>();
                                _logger.LogInformation("Parsed {TotalSites} sites from MCP content, {ValidSites} have valid URLs", sites?.Count ?? 0, validSites.Count);
                                return validSites;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse MCP content format, trying other formats");
                }
            }
            
            // Handle different result formats from MCP server
            if (mcpResult.StartsWith("SHAREPOINT_CARDS:"))
            {
                var jsonData = mcpResult.Replace("SHAREPOINT_CARDS:", "").Trim();
                var sites = JsonSerializer.Deserialize<List<SharePointSiteData>>(jsonData, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
                
                // Filter out any sites with empty URLs
                var validSites = sites?.Where(s => !string.IsNullOrEmpty(s.Url) && !string.IsNullOrEmpty(s.Title)).ToList() ?? new List<SharePointSiteData>();
                _logger.LogInformation("Parsed {TotalSites} sites from MCP result, {ValidSites} have valid URLs", sites?.Count ?? 0, validSites.Count);
                return validSites;
            }
            else if (mcpResult.StartsWith("No SharePoint sites found"))
            {
                return new List<SharePointSiteData>();
            }
            else
            {
                // Try to parse as direct JSON array
                try
                {
                    var sites = JsonSerializer.Deserialize<List<SharePointSiteData>>(mcpResult, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    });
                    
                    // Filter out any sites with empty URLs
                    var validSites = sites?.Where(s => !string.IsNullOrEmpty(s.Url) && !string.IsNullOrEmpty(s.Title)).ToList() ?? new List<SharePointSiteData>();
                    _logger.LogInformation("Parsed {TotalSites} sites from JSON, {ValidSites} have valid URLs", sites?.Count ?? 0, validSites.Count);
                    return validSites;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Could not parse MCP result as SharePoint sites JSON. Result: {Result}", mcpResult);
                    return new List<SharePointSiteData>();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MCP result: {Result}", mcpResult);
            return new List<SharePointSiteData>();
        }
    }

    private string DetermineMatchReason(SharePointSiteData site, string keywords)
    {
        var reasons = new List<string>();
        var keywordLower = keywords.ToLower();
        
        if (site.Title.ToLower().Contains(keywordLower))
            reasons.Add("title");
        if (!string.IsNullOrEmpty(site.Description) && site.Description.ToLower().Contains(keywordLower))
            reasons.Add("description");
            
        return reasons.Any() ? string.Join(" and ", reasons) : "content";
    }

    private string BuildSearchSummary(string query, string timePeriod, List<string> keywords, string searchScope, string sortBy, bool exactMatch)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(query))
            parts.Add($"query '{query}'");
        if (!string.IsNullOrEmpty(timePeriod))
            parts.Add($"time period {timePeriod}");
        if (keywords.Any())
            parts.Add($"keywords [{string.Join(", ", keywords)}]");
        if (searchScope != "all")
            parts.Add($"scope {searchScope}");
        if (exactMatch)
            parts.Add("exact match");
            
        return parts.Any() ? string.Join(", ", parts) : "all sites";
    }

    private class SharePointSiteData
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string WebTemplate { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
} 