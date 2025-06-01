using Microsoft.Identity.Client;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace SemanticKernelPoc.McpServer.Services;

public interface ISharePointSearchService
{
    Task<SharePointSearchResponse> SearchCoffeeNetSitesAsync(SharePointSearchRequest request, string userToken);
}

public class TenantInfo
{
    public string TenantId { get; set; } = "";
    public string SharePointRootUrl { get; set; } = "";
}

public class SharePointSearchService : ISharePointSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SharePointSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConfidentialClientApplication _clientApp;

    public SharePointSearchService(
        HttpClient httpClient,
        ILogger<SharePointSearchService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Initialize MSAL client app for On-Behalf-Of flow - this needs tenant info from config for the app registration
        _clientApp = ConfidentialClientApplicationBuilder
            .Create(_configuration["AzureAd:ClientId"])
            .WithClientSecret(_configuration["AzureAd:ClientSecret"])
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{_configuration["AzureAd:TenantId"]}"))
            .Build();
    }

    public async Task<SharePointSearchResponse> SearchCoffeeNetSitesAsync(SharePointSearchRequest request, string userToken)
    {
        try
        {
            // Get tenant information using Graph API instead of token parsing
            var tenantInfo = await GetTenantInfoFromGraphAsync(userToken);

            var sharePointAccessToken = await GetSharePointTokenAsync(userToken, tenantInfo);
            var searchResults = await ExecuteSearchAsync(sharePointAccessToken, request, tenantInfo);

            var response = ParseSearchResults(searchResults);
            
            // Add query information to response
            response.SearchQuery = BuildSearchQuery(request);
            response.QueryExplanation = GenerateQueryExplanation(request);
            
            return response;
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogWarning(ex, "SharePoint search requires additional user consent for SharePoint access");
            
            // Return a user-friendly error response
            var errorResponse = new SharePointSearchResponse();
            errorResponse.SearchQuery = BuildSearchQuery(request);
            errorResponse.QueryExplanation = "SharePoint access requires additional permissions. Please sign out and sign back in to grant SharePoint access permissions.";
            
            throw new UnauthorizedAccessException(
                "SharePoint search requires additional permissions. The user needs to sign out and sign back in with SharePoint access permissions. " +
                "Required scopes: Sites.Read.All, Sites.Search.All"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CoffeeNet sites");
            throw new InvalidOperationException($"Error searching CoffeeNet sites: {ex.Message}", ex);
        }
    }

    private async Task<TenantInfo> GetTenantInfoFromGraphAsync(string userToken)
    {
        try
        {
            // First, get a Graph API token using the user token
            var graphToken = await GetGraphTokenAsync(userToken);

            // Call Graph API to get organization information
            var orgInfo = await GetOrganizationInfoAsync(graphToken);

            var tenantInfo = new TenantInfo
            {
                TenantId = orgInfo.TenantId,
                SharePointRootUrl = orgInfo.SharePointRootUrl
            };

            _logger.LogInformation("Retrieved tenant info from Graph API - TenantId: {TenantId}, SharePointUrl: {SharePointUrl}",
                tenantInfo.TenantId, tenantInfo.SharePointRootUrl);

            return tenantInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tenant information from Graph API");
            throw;
        }
    }

    private async Task<string> GetGraphTokenAsync(string userToken)
    {
        try
        {
            // Define the scopes we need for Graph API
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            // Create UserAssertion from the incoming token
            var userAssertion = new UserAssertion(userToken);

            // Use On-Behalf-Of flow to get Graph token
            var result = await _clientApp
                .AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync();

            _logger.LogInformation("Successfully acquired Graph API token using On-Behalf-Of flow");
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Graph API token using On-Behalf-Of flow");
            throw;
        }
    }

    private async Task<(string TenantId, string SharePointRootUrl)> GetOrganizationInfoAsync(string graphToken)
    {
        try
        {
            // Call Graph API to get organization information
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/organization");
            request.Headers.Add("Authorization", $"Bearer {graphToken}");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Graph API organization response: {Content}", content);

            response.EnsureSuccessStatusCode();

            var jsonDocument = JsonDocument.Parse(content);
            var organizations = jsonDocument.RootElement.GetProperty("value").EnumerateArray();

            if (!organizations.Any())
            {
                throw new InvalidOperationException("No organization found in Graph API response");
            }

            var org = organizations.First();
            var tenantId = org.GetProperty("id").GetString();

            // Try to get SharePoint root URL from various properties
            string sharePointRootUrl = "";

            // Method 1: Try to get from verifiedDomains (look for the primary domain)
            if (org.TryGetProperty("verifiedDomains", out var verifiedDomains))
            {
                foreach (var domain in verifiedDomains.EnumerateArray())
                {
                    if (domain.TryGetProperty("isDefault", out var isDefault) && isDefault.GetBoolean())
                    {
                        var domainName = domain.GetProperty("name").GetString();
                        if (domainName.EndsWith(".onmicrosoft.com"))
                        {
                            // Extract tenant name from *.onmicrosoft.com
                            var tenantName = domainName.Replace(".onmicrosoft.com", "");
                            sharePointRootUrl = $"https://{tenantName}.sharepoint.com";
                            break;
                        }
                    }
                }
            }

            // Method 2: If we couldn't find it from domains, make a direct call to get SharePoint admin URL
            if (string.IsNullOrEmpty(sharePointRootUrl))
            {
                sharePointRootUrl = await GetSharePointRootUrlFromSPOServiceAsync(graphToken);
            }

            if (string.IsNullOrEmpty(sharePointRootUrl))
            {
                throw new InvalidOperationException("Could not determine SharePoint root URL from Graph API");
            }

            return (tenantId, sharePointRootUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organization information from Graph API");
            throw;
        }
    }

    private async Task<string> GetSharePointRootUrlFromSPOServiceAsync(string graphToken)
    {
        try
        {
            // Alternative: Try to get SharePoint sites to infer the root URL
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/sites/root");
            request.Headers.Add("Authorization", $"Bearer {graphToken}");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Graph API root site response: {Content}", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonDocument = JsonDocument.Parse(content);
                var webUrl = jsonDocument.RootElement.GetProperty("webUrl").GetString();

                // Extract the root URL (e.g., from "https://contoso.sharepoint.com/sites/root" get "https://contoso.sharepoint.com")
                var uri = new Uri(webUrl);
                return $"{uri.Scheme}://{uri.Host}";
            }

            return "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get SharePoint root URL from sites API");
            return "";
        }
    }

    private async Task<string> GetSharePointTokenAsync(string userToken, TenantInfo tenantInfo)
    {
        try
        {
            // Extract the SharePoint host from the root URL
            var uri = new Uri(tenantInfo.SharePointRootUrl);
            
            // Use the original working scope format for SharePoint REST API
            var scopes = new[] { $"{uri.Scheme}://{uri.Host}/.default" };

            // Create UserAssertion from the incoming token
            var userAssertion = new UserAssertion(userToken);

            // Use On-Behalf-Of flow to get SharePoint token
            var result = await _clientApp.AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync();

            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogError(ex, "SharePoint authentication requires additional consent. User needs to re-authenticate with SharePoint permissions.");
            throw new UnauthorizedAccessException(
                "SharePoint access requires additional permissions. Please sign out and sign back in to grant SharePoint access.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire SharePoint access token");
            throw new UnauthorizedAccessException("Failed to access SharePoint. Please try signing in again.", ex);
        }
    }

    private string BuildSearchQuery(SharePointSearchRequest request)
    {
        // Build SharePoint Search query using proper KQL syntax
        var queryParts = new List<string>();
        
        // Start with contentclass:STS_Site to search for SharePoint sites
        queryParts.Add("contentclass:STS_Site");
        
        // Add user query if provided
        if (!string.IsNullOrEmpty(request.Query))
        {
            var processedQuery = ProcessUserQuery(request.Query);
            if (!string.IsNullOrEmpty(processedQuery))
            {
                queryParts.Add(processedQuery);
            }
        }
        
        // Add keywords if provided
        if (request.Keywords != null && request.Keywords.Any())
        {
            var keywordQuery = BuildKeywordQuery(request.Keywords, request.ExactMatch, request.SearchScope);
            if (!string.IsNullOrEmpty(keywordQuery))
            {
                queryParts.Add(keywordQuery);
            }
        }
        
        // Add time-based filtering with proper KQL syntax
        var (createdAfter, createdBefore) = ParseTimePeriod(request.TimePeriod, request.CreatedAfter, request.CreatedBefore);
        if (!string.IsNullOrEmpty(createdAfter))
        {
            queryParts.Add($"Created>={createdAfter}");
        }
        if (!string.IsNullOrEmpty(createdBefore))
        {
            queryParts.Add($"Created<={createdBefore}");
        }
        
        // Combine all query parts using proper KQL AND syntax
        var finalQuery = string.Join(" AND ", queryParts);
        
        _logger.LogInformation("Built SharePoint search query: {Query}", finalQuery);
        _logger.LogInformation("Query parts: {QueryParts}", string.Join(" | ", queryParts));
        return finalQuery;
    }
    
    private string ProcessUserQuery(string userQuery)
    {
        if (string.IsNullOrEmpty(userQuery))
            return string.Empty;
            
        // Remove time-related keywords as they're handled separately
        var cleanedQuery = RemoveTimeKeywords(userQuery);
        
        if (string.IsNullOrEmpty(cleanedQuery))
            return string.Empty;
            
        // For simple keyword searches, use proper KQL syntax
        // If the query contains spaces, treat it as a phrase search
        if (cleanedQuery.Contains(" "))
        {
            return $"\"{cleanedQuery}\"";
        }
        
        return cleanedQuery;
    }

    private string BuildKeywordQuery(List<string> keywords, bool exactMatch, string searchScope)
    {
        if (keywords == null || !keywords.Any())
            return string.Empty;

        var keywordParts = new List<string>();
        
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            
            if (exactMatch || keyword.Contains(" "))
            {
                keywordParts.Add($"\"{keyword}\"");
            }
            else
            {
                keywordParts.Add(keyword);
            }
        }

        if (!keywordParts.Any())
            return string.Empty;

        return keywordParts.Count == 1 ? keywordParts[0] : $"({string.Join(" AND ", keywordParts)})";
    }

    private (string createdAfter, string createdBefore) ParseTimePeriod(string timePeriod, string existingAfter, string existingBefore)
    {
        // Use existing values if provided
        if (!string.IsNullOrEmpty(existingAfter) || !string.IsNullOrEmpty(existingBefore))
        {
            return (existingAfter, existingBefore);
        }

        if (string.IsNullOrEmpty(timePeriod))
        {
            return (string.Empty, string.Empty);
        }

        var now = DateTime.UtcNow;
        var period = timePeriod.ToLower().Trim();

        var (startDate, endDate) = period switch
        {
            "today" => (now.Date, now.Date.AddDays(1)),
            "yesterday" => (now.Date.AddDays(-1), now.Date),
            "this_week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(7 - (int)now.DayOfWeek)),
            "last_week" => (now.Date.AddDays(-(int)now.DayOfWeek - 7), now.Date.AddDays(-(int)now.DayOfWeek)),
            "this_month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            "last_month" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1)),
            _ when period.StartsWith("last_") && period.EndsWith("_days") => ParseLastDays(period, now),
            _ => (DateTime.MinValue, DateTime.MinValue)
        };

        if (startDate == DateTime.MinValue)
        {
            return (string.Empty, string.Empty);
        }

        // Format dates for SharePoint search API (YYYY-MM-DD format)
        var formattedAfter = startDate.ToString("yyyy-MM-dd");
        var formattedBefore = endDate > startDate ? endDate.ToString("yyyy-MM-dd") : string.Empty;
        
        return (formattedAfter, formattedBefore);
    }

    private (DateTime startDate, DateTime endDate) ParseLastDays(string period, DateTime now)
    {
        var match = System.Text.RegularExpressions.Regex.Match(period, @"last_(\d+)_days");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
        {
            return (now.Date.AddDays(-days), now.Date);
        }
        return (DateTime.MinValue, DateTime.MinValue);
    }

    private string RemoveTimeKeywords(string query)
    {
        if (string.IsNullOrEmpty(query))
            return string.Empty;

        // Remove common time-related keywords from the search query
        var timeKeywords = new[]
        {
            @"\blast\s+\d+\s+(day|days|week|weeks|month|months|year|years)\b",
            @"\b(today|yesterday|this\s+week|last\s+week|this\s+month|last\s+month)\b",
            @"\b(recent|latest|new|newest)\b"
        };

        var cleanedQuery = query;
        foreach (var pattern in timeKeywords)
        {
            cleanedQuery = System.Text.RegularExpressions.Regex.Replace(
                cleanedQuery, pattern, " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return cleanedQuery.Trim();
    }

    private async Task<dynamic> ExecuteSearchAsync(string accessToken, SharePointSearchRequest request, TenantInfo tenantInfo)
    {
        var searchQuery = BuildSearchQuery(request);
        var searchUrl = BuildSearchUrl(searchQuery, request, tenantInfo);
        
        _logger.LogInformation("Executing SharePoint search: {Query} (Max: {MaxResults})", searchQuery, request.MaxResults);
        
        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        httpRequestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        httpRequestMessage.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        httpRequestMessage.Headers.Add("User-Agent", "SemanticKernelPoc/1.0");
        
        try
        {
            var response = await _httpClient.SendAsync(httpRequestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SharePoint Search failed. Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);
                    
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException("SharePoint Search access denied. Check permissions and token scopes.");
                }
                
                throw new InvalidOperationException($"SharePoint Search request failed: {response.StatusCode} - {responseContent}");
            }
            
            var searchResults = JsonSerializer.Deserialize<dynamic>(responseContent);
            _logger.LogInformation("SharePoint search completed successfully");
            
            return searchResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SharePoint search request");
            throw;
        }
    }

    private string BuildSearchUrl(string searchQuery, SharePointSearchRequest request, TenantInfo tenantInfo)
    {
        var searchUrlBuilder = new StringBuilder();
        searchUrlBuilder.Append($"{tenantInfo.SharePointRootUrl}/_api/search/query");
        searchUrlBuilder.Append($"?querytext='{Uri.EscapeDataString(searchQuery)}'");
        searchUrlBuilder.Append($"&rowlimit={Math.Min(request.MaxResults, 500)}");
        
        // Add select properties for sites
        var selectProperties = new[] { "Title", "Path", "Description", "Created", "LastModifiedTime", "SiteLogo", "WebTemplate", "Rank" };
        var selectPropertiesEncoded = Uri.EscapeDataString(string.Join(",", selectProperties));
        searchUrlBuilder.Append($"&selectproperties='{selectPropertiesEncoded}'");
        
        // Add sorting if specified
        if (!string.IsNullOrEmpty(request.SortBy) && request.SortBy != "relevance")
        {
            var sortDirection = request.SortOrder.ToLower() == "asc" ? "ascending" : "descending";
            var sortProperty = MapSortProperty(request.SortBy);
            var sortList = $"{sortProperty}:{sortDirection}";
            var sortListEncoded = Uri.EscapeDataString(sortList);
            searchUrlBuilder.Append($"&sortlist='{sortListEncoded}'");
        }
        
        // Add other search parameters
        searchUrlBuilder.Append("&trimduplicates=true");
        searchUrlBuilder.Append("&enablequeryrules=true");
        searchUrlBuilder.Append("&enablestemming=true");
        
        return searchUrlBuilder.ToString();
    }
    
    private string MapSortProperty(string sortBy)
    {
        return sortBy.ToLower() switch
        {
            "created" => "Created",
            "modified" => "LastModifiedTime", 
            "title" => "Title",
            _ => "Rank"
        };
    }

    private SharePointSearchResponse ParseSearchResults(dynamic searchResults)
    {
        var response = new SharePointSearchResponse();

        try
        {
            JsonElement jsonElement;
            if (searchResults is JsonElement element)
            {
                jsonElement = element;
            }
            else
            {
                var jsonString = JsonSerializer.Serialize(searchResults);
                jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
            }
            
            // SharePoint REST API format: direct access to PrimaryQueryResult
            if (!jsonElement.TryGetProperty("PrimaryQueryResult", out JsonElement primaryQueryResult))
            {
                _logger.LogWarning("No PrimaryQueryResult found in SharePoint search response");
                return response;
            }

            if (!primaryQueryResult.TryGetProperty("RelevantResults", out JsonElement relevantResults))
            {
                _logger.LogWarning("No RelevantResults found in search response");
                return response;
            }

            if (!relevantResults.TryGetProperty("Table", out JsonElement table))
            {
                _logger.LogWarning("No Table found in search response");
                return response;
            }

            // SharePoint format: Table.Rows is a direct array
            if (!table.TryGetProperty("Rows", out JsonElement rowsArray) || rowsArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("No Rows array found in Table structure");
                return response;
            }

            _logger.LogInformation("Processing {RowCount} SharePoint search results", rowsArray.GetArrayLength());

            // Parse each result row
            foreach (JsonElement row in rowsArray.EnumerateArray())
            {
                try
                {
                    var site = ParseSiteFromRow(row);
                    if (site != null && !string.IsNullOrEmpty(site.Url))
                    {
                        response.Sites.Add(site);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing individual search result row");
                    continue;
                }
            }

            // Set total results count
            response.TotalResults = relevantResults.TryGetProperty("TotalRows", out JsonElement totalRowsElement) &&
                                    totalRowsElement.TryGetInt32(out int totalRows)
                                    ? totalRows
                                    : response.Sites.Count;
            
            _logger.LogInformation("Successfully parsed {SiteCount} sites from SharePoint search results", response.Sites.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing SharePoint search results");
            throw new InvalidOperationException($"Failed to parse SharePoint search results: {ex.Message}", ex);
        }

        return response;
    }

    private SharePointSite ParseSiteFromRow(JsonElement row)
    {
        var site = new SharePointSite();
        
        // SharePoint format: Cells is a direct array
        if (!row.TryGetProperty("Cells", out JsonElement cellsArray) || cellsArray.ValueKind != JsonValueKind.Array)
        {
            return site;
        }

        // Parse each cell key-value pair
        foreach (JsonElement cell in cellsArray.EnumerateArray())
        {
            if (!cell.TryGetProperty("Key", out JsonElement keyElement) ||
                !cell.TryGetProperty("Value", out JsonElement valueElement))
            {
                continue;
            }

            var key = keyElement.GetString();
            var value = valueElement.ValueKind == JsonValueKind.Null ? null : valueElement.GetString();

            if (string.IsNullOrEmpty(key)) continue;

            switch (key)
            {
                case "Title":
                    site.Title = value ?? "";
                    break;
                case "Path":
                    site.Url = value ?? "";
                    break;
                case "Description":
                    site.Description = value ?? "";
                    break;
                case "Created":
                    if (DateTime.TryParse(value, out DateTime createdDate))
                    {
                        site.Created = createdDate;
                    }
                    break;
                case "LastModifiedTime":
                    if (DateTime.TryParse(value, out DateTime modifiedDate))
                    {
                        site.LastModified = modifiedDate;
                    }
                    break;
                case "WebTemplate":
                    site.WebTemplate = value ?? "";
                    break;
                case "Rank":
                    if (double.TryParse(value, out double rank))
                    {
                        site.Relevance = rank;
                    }
                    break;
            }
        }

        return site;
    }

    private string GenerateQueryExplanation(SharePointSearchRequest request)
    {
        var parts = new List<string> { "SharePoint sites" };

        if (!string.IsNullOrEmpty(request.Query))
        {
            parts.Add($"matching '{request.Query}'");
        }

        if (!string.IsNullOrEmpty(request.TimePeriod))
        {
            parts.Add($"from {request.TimePeriod}");
        }

        if (request.Keywords?.Any() == true)
        {
            parts.Add($"with keywords: {string.Join(", ", request.Keywords)}");
        }

        return string.Join(" ", parts);
    }
}