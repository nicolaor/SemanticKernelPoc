using Microsoft.Identity.Client;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

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
            var searchQuery = BuildSearchQuery(request);
            var searchResults = await ExecuteSearchAsync(sharePointAccessToken, searchQuery, tenantInfo);

            return ParseSearchResults(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for CoffeeNet sites");
            throw;
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
            // Extract the SharePoint host from the root URL to build the scope
            var uri = new Uri(tenantInfo.SharePointRootUrl);
            var scopes = new[] { $"{uri.Scheme}://{uri.Host}/.default" };

            // Create UserAssertion from the incoming token
            var userAssertion = new UserAssertion(userToken);

            // Use On-Behalf-Of flow to get SharePoint token
            var result = await _clientApp
                .AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync();

            _logger.LogInformation("Successfully acquired SharePoint token for {SharePointUrl} using On-Behalf-Of flow", tenantInfo.SharePointRootUrl);
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire SharePoint token using On-Behalf-Of flow for {SharePointUrl}", tenantInfo.SharePointRootUrl);
            throw;
        }
    }

    private string BuildSearchQuery(SharePointSearchRequest request)
    {
        // Start with a basic query to find sites
        var queryParts = new List<string> { "contentclass:STS_Site" };

        // Add user query if provided - use proper SharePoint query syntax
        if (!string.IsNullOrEmpty(request.Query))
        {
            // Escape special characters and add as a phrase search or individual terms
            var userQuery = request.Query.Trim();
            if (userQuery.Contains(' '))
            {
                // Multi-word query - use phrase search
                queryParts.Add($"\"{userQuery}\"");
            }
            else
            {
                // Single word - add as-is
                queryParts.Add(userQuery);
            }
        }

        // Add date filtering if provided
        if (!string.IsNullOrEmpty(request.CreatedAfter))
        {
            if (DateTime.TryParse(request.CreatedAfter, out var afterDate))
            {
                // SharePoint uses Write property for last modified date
                var dateString = afterDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                queryParts.Add($"Write>={dateString}");
            }
        }

        if (!string.IsNullOrEmpty(request.CreatedBefore))
        {
            if (DateTime.TryParse(request.CreatedBefore, out var beforeDate))
            {
                var dateString = beforeDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                queryParts.Add($"Write<={dateString}");
            }
        }

        // Combine all query parts with AND
        var finalQuery = string.Join(" AND ", queryParts);

        _logger.LogInformation("Built search query: {Query}", finalQuery);
        return finalQuery;
    }

    private async Task<dynamic> ExecuteSearchAsync(string accessToken, string query, TenantInfo tenantInfo)
    {
        // Build the search URL for GET request using the dynamically retrieved SharePoint URL
        var encodedQuery = Uri.EscapeDataString(query);
        
        // Use a more comprehensive search URL with proper parameters
        var searchUrl = $"{tenantInfo.SharePointRootUrl}/_api/search/query?" +
                       $"querytext='{encodedQuery}'" +
                       $"&rowlimit=50" +
                       $"&selectproperties='Title,Path,Description,Write,LastModifiedTime,WebTemplate,SiteDescription,CN365TemplateIdOWSText,CN365TemplateId'" +
                       $"&trimduplicates=true";

        _logger.LogInformation("=== SHAREPOINT GET REQUEST ===");
        _logger.LogInformation("URL: {SearchUrl}", searchUrl);
        _logger.LogInformation("Query: {Query}", query);
        _logger.LogInformation("SharePoint Root URL: {SharePointUrl} (retrieved from Graph API)", tenantInfo.SharePointRootUrl);

        // Use the injected HttpClient from HttpClientFactory
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata=verbose");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SemanticKernelPoc/1.0");

        _logger.LogInformation("Making HTTP GET request using injected HttpClient...");

        try
        {
            var response = await _httpClient.GetAsync(searchUrl);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Response length: {Length}", content.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SharePoint API returned error. Status: {StatusCode}, Content: {Content}", response.StatusCode, content);
                
                // Check for specific authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("SharePoint API returned 401 Unauthorized. Please check your authentication token and permissions.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException("SharePoint API returned 403 Forbidden. You may not have permission to search SharePoint sites.");
                }
            }

            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<dynamic>(content);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP GET request failed for SharePoint URL {SharePointUrl}", tenantInfo.SharePointRootUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SharePoint GET request for URL {SharePointUrl}", tenantInfo.SharePointRootUrl);
            throw;
        }
    }

    private SharePointSearchResponse ParseSearchResults(dynamic searchResults)
    {
        var response = new SharePointSearchResponse
        {
            Sites = new List<SharePointSite>(),
            TotalResults = 0
        };

        try
        {
            JsonElement jsonElement;

            // Handle both JsonElement and string cases
            if (searchResults is JsonElement element)
            {
                jsonElement = element;
            }
            else if (searchResults is string jsonStr)
            {
                jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonStr);
            }
            else
            {
                // Convert to string first, then parse
                var serializedJson = JsonSerializer.Serialize(searchResults);
                jsonElement = JsonSerializer.Deserialize<JsonElement>(serializedJson);
            }

            _logger.LogInformation("Parsing SharePoint search results from JsonElement");

            // Navigate through the SharePoint REST API response structure
            JsonElement? rowsElement = null;

            // Try the expected SharePoint REST API format: d.query.PrimaryQueryResult.RelevantResults.Table.Rows
            if (jsonElement.TryGetProperty("d", out var dWrapper) &&
                dWrapper.TryGetProperty("query", out var queryWrapper) &&
                queryWrapper.TryGetProperty("PrimaryQueryResult", out var primaryResult) &&
                primaryResult.TryGetProperty("RelevantResults", out var relevantResults) &&
                relevantResults.TryGetProperty("Table", out var table) &&
                table.TryGetProperty("Rows", out var rows) &&
                rows.TryGetProperty("results", out var rowsResults))
            {
                rowsElement = rowsResults;
                _logger.LogInformation("Found results using SharePoint REST API format with 'd' wrapper");
                
                // Also get total row count if available
                if (relevantResults.TryGetProperty("TotalRows", out var totalRowsProperty))
                {
                    response.TotalResults = totalRowsProperty.GetInt32();
                }
            }
            // Try without 'd' wrapper (direct format)
            else if (jsonElement.TryGetProperty("query", out var directQuery) &&
                     directQuery.TryGetProperty("PrimaryQueryResult", out var directPrimary) &&
                     directPrimary.TryGetProperty("RelevantResults", out var directRelevant) &&
                     directRelevant.TryGetProperty("Table", out var directTable) &&
                     directTable.TryGetProperty("Rows", out var directRows) &&
                     directRows.TryGetProperty("results", out var directRowsResults))
            {
                rowsElement = directRowsResults;
                _logger.LogInformation("Found results using direct SharePoint REST API format");
                
                if (directRelevant.TryGetProperty("TotalRows", out var directTotalRows))
                {
                    response.TotalResults = directTotalRows.GetInt32();
                }
            }
            else
            {
                _logger.LogWarning("Could not find results in expected SharePoint format. Response structure: {Keys}",
                    string.Join(", ", jsonElement.EnumerateObject().Select(p => p.Name)));
                
                // Log the actual JSON structure for debugging
                var debugJson = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogDebug("Full response structure: {Json}", debugJson);
                return response; // Return empty response
            }

            if (rowsElement.HasValue && rowsElement.Value.ValueKind == JsonValueKind.Array)
            {
                _logger.LogInformation("Processing {Count} result rows", rowsElement.Value.GetArrayLength());

                foreach (var row in rowsElement.Value.EnumerateArray())
                {
                    var site = new SharePointSite();

                    // Navigate to Cells.results array
                    if (row.TryGetProperty("Cells", out var cellsWrapper) &&
                        cellsWrapper.TryGetProperty("results", out var cellsArray))
                    {
                        foreach (var cell in cellsArray.EnumerateArray())
                        {
                            if (cell.TryGetProperty("Key", out var keyProp) &&
                                cell.TryGetProperty("Value", out var valueProp))
                            {
                                var key = keyProp.GetString();
                                
                                // Handle null values properly
                                string value = null;
                                if (valueProp.ValueKind == JsonValueKind.String)
                                {
                                    value = valueProp.GetString();
                                }
                                else if (valueProp.ValueKind == JsonValueKind.Null)
                                {
                                    value = null;
                                }
                                else
                                {
                                    value = valueProp.ToString();
                                }

                                switch (key)
                                {
                                    case "Title":
                                        site.Title = value ?? "";
                                        break;
                                    case "Path":
                                        site.Url = value ?? "";
                                        break;
                                    case "Description":
                                    case "SiteDescription":
                                        site.Description = value ?? "";
                                        break;
                                    case "Write":
                                    case "LastModifiedTime":
                                        if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, out DateTime modified))
                                            site.Created = modified;
                                        break;
                                    case "WebTemplate":
                                        site.WebTemplate = value ?? "";
                                        break;
                                    case "CN365TemplateIdOWSText":
                                    case "CN365TemplateId":
                                        site.TemplateId = value ?? "";
                                        break;
                                }
                            }
                        }
                    }

                    // Only add sites that have at least a title and URL
                    if (!string.IsNullOrEmpty(site.Title) && !string.IsNullOrEmpty(site.Url))
                    {
                        response.Sites.Add(site);
                        _logger.LogDebug("Added site: {Title} - {Url}", site.Title, site.Url);
                    }
                    else
                    {
                        _logger.LogDebug("Skipped site due to missing title or URL: Title='{Title}', Url='{Url}'", 
                            site.Title, site.Url);
                    }
                }

                // If we didn't get TotalResults from the API response, use the actual count
                if (response.TotalResults == 0)
                {
                    response.TotalResults = response.Sites.Count;
                }

                response.HasMore = response.Sites.Count < response.TotalResults;
                
                _logger.LogInformation("Successfully parsed {Count} SharePoint sites out of {Total} total results", 
                    response.Sites.Count, response.TotalResults);
            }
            else
            {
                _logger.LogWarning("Rows element is not an array or is null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing SharePoint search results");
        }

        return response;
    }
}