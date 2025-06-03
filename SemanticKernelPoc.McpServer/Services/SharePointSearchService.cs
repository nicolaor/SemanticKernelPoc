using Microsoft.Identity.Client;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace SemanticKernelPoc.McpServer.Services;

public interface ISharePointSearchService
{
    Task<SharePointSearchResponse> SearchSharePointSitesAsync(SharePointSearchRequest request, string userToken);
    Task<TenantInfo> GetTenantInfoFromGraphAsync(string userToken);
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

    public async Task<SharePointSearchResponse> SearchSharePointSitesAsync(SharePointSearchRequest request, string userToken)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("🔍 === SHAREPOINT SEARCH SERVICE START === RequestId: {RequestId}", requestId);
            _logger.LogInformation("📋 SERVICE INPUT for request {RequestId}:", requestId);
            _logger.LogInformation("   Query: '{Query}'", request.Query ?? "(null)");
            _logger.LogInformation("   MaxResults: {MaxResults}", request.MaxResults);
            _logger.LogInformation("   CreatedAfter: '{CreatedAfter}'", request.CreatedAfter ?? "(null)");
            _logger.LogInformation("   CreatedBefore: '{CreatedBefore}'", request.CreatedBefore ?? "(null)");
            _logger.LogInformation("   SortBy: '{SortBy}'", request.SortBy ?? "(null)");
            _logger.LogInformation("   SortOrder: '{SortOrder}'", request.SortOrder ?? "(null)");
            _logger.LogInformation("   TimePeriod: '{TimePeriod}'", request.TimePeriod ?? "(null)");
            _logger.LogInformation("   Received user token length: {TokenLength} characters", userToken?.Length ?? 0);
            
            // Validate the incoming token
            if (string.IsNullOrWhiteSpace(userToken))
            {
                _logger.LogError("❌ Request {RequestId} VALIDATION FAILED: User token is null or empty", requestId);
                throw new ArgumentException("User token is null or empty", nameof(userToken));
            }

            // Log token details (first/last chars only for security)
            var tokenStart = userToken.Length > 10 ? userToken.Substring(0, 10) : userToken;
            var tokenEnd = userToken.Length > 10 ? userToken.Substring(userToken.Length - 10) : "";
            _logger.LogInformation("🔑 Request {RequestId} token validation - starts with: {TokenStart}..., ends with: ...{TokenEnd}", 
                requestId, tokenStart, tokenEnd);
            
            // Validate token structure
            if (userToken.Contains("."))
            {
                var tokenParts = userToken.Split('.');
                _logger.LogInformation("🔍 Request {RequestId} token structure - Parts: {PartsCount} (expected: 3 for JWT)", 
                    requestId, tokenParts.Length);
            }
            else
            {
                _logger.LogWarning("⚠️ Request {RequestId} token does not appear to be JWT format (no dots found)", requestId);
            }
            
            // Get tenant information using Graph API instead of token parsing
            _logger.LogInformation("🏢 Request {RequestId} STEP 1: Getting tenant information from Graph API...", requestId);
            TenantInfo tenantInfo;
            try
            {
                var tenantStopwatch = System.Diagnostics.Stopwatch.StartNew();
                tenantInfo = await GetTenantInfoFromGraphAsync(userToken);
                tenantStopwatch.Stop();
                
                _logger.LogInformation("✅ Request {RequestId} STEP 1 completed in {ElapsedMs}ms:", requestId, tenantStopwatch.ElapsedMilliseconds);
                _logger.LogInformation("   Tenant ID: {TenantId}", tenantInfo.TenantId);
                _logger.LogInformation("   SharePoint URL: {SharePointUrl}", tenantInfo.SharePointRootUrl);
            }
            catch (Exception tenantEx)
            {
                _logger.LogError(tenantEx, "❌ Request {RequestId} STEP 1 FAILED: Tenant information retrieval failed", requestId);
                _logger.LogError("   Exception Type: {ExceptionType}", tenantEx.GetType().Name);
                _logger.LogError("   Exception Message: {Message}", tenantEx.Message);
                throw;
            }

            _logger.LogInformation("🔐 Request {RequestId} STEP 2: Acquiring SharePoint access token...", requestId);
            string sharePointAccessToken;
            try
            {
                var tokenStopwatch = System.Diagnostics.Stopwatch.StartNew();
                sharePointAccessToken = await GetSharePointTokenAsync(userToken, tenantInfo);
                tokenStopwatch.Stop();
                
                _logger.LogInformation("✅ Request {RequestId} STEP 2 completed in {ElapsedMs}ms: SharePoint token acquired (length: {TokenLength})", 
                    requestId, tokenStopwatch.ElapsedMilliseconds, sharePointAccessToken?.Length ?? 0);
                
                if (sharePointAccessToken != null && sharePointAccessToken.Length > 20)
                {
                    var spTokenPreview = $"{sharePointAccessToken[..10]}...{sharePointAccessToken[^10..]}";
                    _logger.LogInformation("🔑 Request {RequestId} SharePoint token preview: {TokenPreview}", requestId, spTokenPreview);
                }
            }
            catch (Exception tokenEx)
            {
                _logger.LogError(tokenEx, "❌ Request {RequestId} STEP 2 FAILED: SharePoint token acquisition failed", requestId);
                _logger.LogError("   Exception Type: {ExceptionType}", tokenEx.GetType().Name);
                _logger.LogError("   Exception Message: {Message}", tokenEx.Message);
                throw;
            }

            _logger.LogInformation("🔍 Request {RequestId} STEP 3: Executing SharePoint search...", requestId);
            object searchResults;
            try
            {
                var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
                searchResults = await ExecuteSearchAsync(sharePointAccessToken, request, tenantInfo);
                searchStopwatch.Stop();
                
                _logger.LogInformation("✅ Request {RequestId} STEP 3 completed in {ElapsedMs}ms: Search executed successfully", 
                    requestId, searchStopwatch.ElapsedMilliseconds);
                
                // Log search results details
                if (searchResults != null)
                {
                    _logger.LogInformation("📊 Request {RequestId} raw search results received:", requestId);
                    _logger.LogInformation("   Result type: {ResultType}", searchResults.GetType().Name);
                    
                    try
                    {
                        var resultsJson = JsonSerializer.Serialize(searchResults);
                        _logger.LogInformation("   Result size: {Size} characters", resultsJson.Length);
                        
                        if (resultsJson.Length <= 1000)
                        {
                            _logger.LogInformation("   Full results: {Results}", resultsJson);
                        }
                        else
                        {
                            _logger.LogInformation("   Results preview (first 1000 chars): {ResultsPreview}...", resultsJson[..1000]);
                        }
                    }
                    catch (Exception serEx)
                    {
                        _logger.LogWarning("⚠️ Request {RequestId} failed to serialize search results for logging: {Error}", 
                            requestId, serEx.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Request {RequestId} search returned null results", requestId);
                }
            }
            catch (Exception searchEx)
            {
                _logger.LogError(searchEx, "❌ Request {RequestId} STEP 3 FAILED: SharePoint search execution failed", requestId);
                _logger.LogError("   Exception Type: {ExceptionType}", searchEx.GetType().Name);
                _logger.LogError("   Exception Message: {Message}", searchEx.Message);
                _logger.LogError("   Stack Trace: {StackTrace}", searchEx.StackTrace);
                throw;
            }

            _logger.LogInformation("🔄 Request {RequestId} STEP 4: Parsing search results...", requestId);
            SharePointSearchResponse response;
            try
            {
                var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                response = ParseSearchResults(searchResults);
                parseStopwatch.Stop();
                
                _logger.LogInformation("✅ Request {RequestId} STEP 4 completed in {ElapsedMs}ms: Results parsed successfully", 
                    requestId, parseStopwatch.ElapsedMilliseconds);
                
                // Add query information to response
                response.SearchQuery = BuildSearchQuery(request);
                response.QueryExplanation = GenerateQueryExplanation(request);
                
                _logger.LogInformation("📊 Request {RequestId} FINAL RESPONSE:", requestId);
                _logger.LogInformation("   Sites found: {SitesCount}", response.Sites?.Count ?? 0);
                _logger.LogInformation("   Search query: '{SearchQuery}'", response.SearchQuery ?? "(null)");
                _logger.LogInformation("   Query explanation: '{QueryExplanation}'", response.QueryExplanation ?? "(null)");
                
                if (response.Sites != null && response.Sites.Any())
                {
                    _logger.LogInformation("📝 Request {RequestId} site details:", requestId);
                    for (int i = 0; i < Math.Min(response.Sites.Count, 3); i++)
                    {
                        var site = response.Sites[i];
                        _logger.LogInformation("   Site {Index}: '{Title}' - {Url}", i + 1, site.Title ?? "(no title)", site.Url ?? "(no url)");
                    }
                    
                    if (response.Sites.Count > 3)
                    {
                        _logger.LogInformation("   ... and {MoreCount} more sites", response.Sites.Count - 3);
                    }
                }
                else
                {
                    _logger.LogInformation("📝 Request {RequestId} NO SITES FOUND in response", requestId);
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "❌ Request {RequestId} STEP 4 FAILED: Results parsing failed", requestId);
                _logger.LogError("   Exception Type: {ExceptionType}", parseEx.GetType().Name);
                _logger.LogError("   Exception Message: {Message}", parseEx.Message);
                throw;
            }
                
            _logger.LogInformation("✅ === SHAREPOINT SEARCH SERVICE COMPLETED === RequestId: {RequestId} - Found {Count} sites", 
                requestId, response.Sites?.Count ?? 0);
            return response;
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogWarning("❌ Request {RequestId} SharePoint search requires additional user consent - Error: {ErrorCode}", 
                requestId, ex.ErrorCode);
            
            // Return a user-friendly error response
            var errorResponse = new SharePointSearchResponse();
            errorResponse.SearchQuery = BuildSearchQuery(request);
            errorResponse.QueryExplanation = "SharePoint access requires additional permissions. Please sign out and sign back in to grant SharePoint access permissions.";
            
            throw new UnauthorizedAccessException(
                $"SharePoint search requires additional permissions. Error: {ex.ErrorCode}. " +
                "The user needs to sign out and sign back in with SharePoint access permissions. " +
                "Required scopes: Sites.Read.All, Sites.Search.All"
            );
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "❌ Request {RequestId} SharePoint search authorization failed", requestId);
            throw; // Re-throw as-is since it's already properly formatted
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CRITICAL ERROR in SharePoint search service for request {RequestId}", requestId);
            _logger.LogError("   Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("   Full exception: {FullException}", ex.ToString());
            throw new InvalidOperationException($"Error searching SharePoint sites: {ex.Message}", ex);
        }
    }

    public async Task<TenantInfo> GetTenantInfoFromGraphAsync(string userToken)
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
            _logger.LogInformation("🔄 Acquiring Graph API token using On-Behalf-Of flow...");
            _logger.LogInformation("🔍 Input token validation - Length: {TokenLength}, Is null/empty: {IsEmpty}", 
                userToken?.Length ?? 0, string.IsNullOrWhiteSpace(userToken));
            
            // Log some basic token structure validation (without exposing the token)
            if (!string.IsNullOrWhiteSpace(userToken))
            {
                var tokenParts = userToken.Split('.');
                _logger.LogInformation("🔍 Token structure - Parts count: {PartsCount}, Expected: 3 (header.payload.signature)", tokenParts.Length);
                
                if (tokenParts.Length >= 2)
                {
                    try
                    {
                        // Try to decode the header to see token type (without logging sensitive data)
                        var headerBytes = Convert.FromBase64String(AddPadding(tokenParts[0]));
                        var headerJson = System.Text.Encoding.UTF8.GetString(headerBytes);
                        _logger.LogInformation("🔍 Token header structure validated - Type appears to be JWT");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Token header validation failed - May not be valid JWT: {Error}", ex.Message);
                    }
                }
            }
            
            // Define the scopes we need for Graph API
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            _logger.LogInformation("📋 Requested scopes: {Scopes}", string.Join(", ", scopes));

            // Log MSAL configuration details
            _logger.LogInformation("🔧 MSAL Configuration - Client ID: {ClientId}, Authority: {Authority}", 
                _clientApp.AppConfig.ClientId, _clientApp.Authority);

            // Create UserAssertion from the incoming token
            _logger.LogInformation("🔑 Creating UserAssertion for token exchange...");
            var userAssertion = new UserAssertion(userToken);
            _logger.LogInformation("✅ UserAssertion created successfully");

            // Log token cache status
            var accounts = await _clientApp.GetAccountsAsync();
            _logger.LogInformation("🗃️ MSAL token cache status - Accounts found: {AccountCount}", accounts?.Count() ?? 0);

            // Use On-Behalf-Of flow to get Graph token
            _logger.LogInformation("🔄 Executing OBO flow for Graph API...");
            var tokenBuilder = _clientApp.AcquireTokenOnBehalfOf(scopes, userAssertion);
            
            // Add correlation ID for tracking
            var correlationId = Guid.NewGuid();
            tokenBuilder.WithCorrelationId(correlationId);
            _logger.LogInformation("🔗 OBO request correlation ID: {CorrelationId}", correlationId);

            var result = await tokenBuilder.ExecuteAsync();

            _logger.LogInformation("✅ Successfully acquired Graph API token - Length: {TokenLength}, Expires: {ExpiresOn}, Source: {Source}, Scopes: {Scopes}", 
                result.AccessToken?.Length ?? 0, result.ExpiresOn, result.AuthenticationResultMetadata?.TokenSource, string.Join(", ", result.Scopes ?? new string[0]));
            
            // Log account information (non-sensitive data only)
            if (result.Account != null)
            {
                _logger.LogInformation("👤 Token account info - Username: {Username}, Environment: {Environment}, HomeAccountId: {HomeAccountId}", 
                    result.Account.Username, result.Account.Environment, result.Account.HomeAccountId?.Identifier);
            }
            
            return result.AccessToken;
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogError("❌ Graph token acquisition requires UI interaction - Error: {ErrorCode}", ex.ErrorCode);
            throw;
        }
        catch (Microsoft.Identity.Client.MsalServiceException ex)
        {
            _logger.LogError("❌ MSAL service error during Graph token acquisition - Error: {ErrorCode}", ex.ErrorCode);
            throw;
        }
        catch (Microsoft.Identity.Client.MsalClientException ex)
        {
            _logger.LogError("❌ MSAL client error during Graph token acquisition - Error: {ErrorCode}", ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to acquire Graph API token - Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("🔍 Full exception details: {ExceptionDetails}", ex.ToString());
            throw;
        }
    }

    // Helper method to add padding for base64 decoding
    private static string AddPadding(string input)
    {
        var padded = input;
        var mod = input.Length % 4;
        if (mod != 0)
        {
            padded += new string('=', 4 - mod);
        }
        return padded;
    }

    private async Task<(string TenantId, string SharePointRootUrl)> GetOrganizationInfoAsync(string graphToken)
    {
        try
        {
            _logger.LogInformation("🏢 Calling Graph API to get organization information...");
            _logger.LogInformation("🔍 Graph token validation - Length: {TokenLength}, Valid: {IsValid}", 
                graphToken?.Length ?? 0, !string.IsNullOrWhiteSpace(graphToken));
            
            // Call Graph API to get organization information
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/organization");
            request.Headers.Add("Authorization", $"Bearer {graphToken}");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "SemanticKernelPoc-MCP/1.0");

            _logger.LogInformation("📤 Sending request to Graph API organization endpoint");
            _logger.LogInformation("🔗 Request URL: {RequestUrl}", request.RequestUri);
            _logger.LogInformation("📋 Request headers count: {HeaderCount}", request.Headers.Count());
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("📥 Graph API organization response - Status: {StatusCode}, Content Length: {ContentLength}", 
                response.StatusCode, content?.Length ?? 0);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Graph API organization call failed - Status: {StatusCode}, Reason: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                _logger.LogError("❌ Response content: {Content}", content);
                _logger.LogError("❌ Response headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
            }

            response.EnsureSuccessStatusCode();

            _logger.LogInformation("✅ Graph API organization response received successfully");
            _logger.LogInformation("🔍 Parsing JSON response...");
            
            var jsonDocument = JsonDocument.Parse(content);
            _logger.LogInformation("✅ JSON parsed successfully");
            
            if (!jsonDocument.RootElement.TryGetProperty("value", out var valueProperty))
            {
                _logger.LogError("❌ No 'value' property found in organization response");
                throw new InvalidOperationException("Invalid organization response format - missing 'value' property");
            }
            
            var organizations = valueProperty.EnumerateArray();
            _logger.LogInformation("🔍 Organizations array found with {Count} items", organizations.Count());

            if (!organizations.Any())
            {
                _logger.LogError("❌ No organizations found in Graph API response");
                throw new InvalidOperationException("No organization found in Graph API response");
            }

            var org = organizations.First();
            _logger.LogInformation("✅ Using first organization from response");
            
            if (!org.TryGetProperty("id", out var idProperty))
            {
                _logger.LogError("❌ Organization 'id' property not found");
                throw new InvalidOperationException("Organization ID not found in response");
            }
            
            var tenantId = idProperty.GetString();
            _logger.LogInformation("🆔 Found tenant ID: {TenantId}", tenantId);

            // Try to get SharePoint root URL from various properties
            string sharePointRootUrl = "";

            // Method 1: Try to get from verifiedDomains (look for the primary domain)
            _logger.LogInformation("🔍 Attempting to extract SharePoint URL from verified domains...");
            if (org.TryGetProperty("verifiedDomains", out var verifiedDomains))
            {
                _logger.LogInformation("✅ Found verifiedDomains property with {Count} domains", verifiedDomains.GetArrayLength());
                
                foreach (var domain in verifiedDomains.EnumerateArray())
                {
                    var domainName = domain.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : "unknown";
                    var isDefault = domain.TryGetProperty("isDefault", out var isDefaultProperty) && isDefaultProperty.GetBoolean();
                    
                    _logger.LogInformation("🌐 Processing domain: {DomainName}, IsDefault: {IsDefault}", domainName, isDefault);
                    
                    if (isDefault)
                    {
                        _logger.LogInformation("🌐 Found default domain: {DomainName}", domainName);
                        
                        if (domainName.EndsWith(".onmicrosoft.com"))
                        {
                            // Extract tenant name from *.onmicrosoft.com
                            var tenantName = domainName.Replace(".onmicrosoft.com", "");
                            sharePointRootUrl = $"https://{tenantName}.sharepoint.com";
                            _logger.LogInformation("✅ Constructed SharePoint URL from domain: {SharePointUrl}", sharePointRootUrl);
                            break;
                        }
                        else
                        {
                            _logger.LogInformation("⚠️ Default domain is not .onmicrosoft.com format: {DomainName}", domainName);
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No verifiedDomains property found in organization response");
            }

            // Method 2: If we couldn't find it from domains, make a direct call to get SharePoint admin URL
            if (string.IsNullOrEmpty(sharePointRootUrl))
            {
                _logger.LogInformation("🔄 Attempting to get SharePoint URL from root site API...");
                try
                {
                    sharePointRootUrl = await GetSharePointRootUrlFromSPOServiceAsync(graphToken);
                    
                    if (!string.IsNullOrEmpty(sharePointRootUrl))
                    {
                        _logger.LogInformation("✅ Retrieved SharePoint URL from root site: {SharePointUrl}", sharePointRootUrl);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ SharePoint root site API returned empty URL");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to get SharePoint URL from root site API");
                }
            }

            if (string.IsNullOrEmpty(sharePointRootUrl))
            {
                _logger.LogError("❌ Could not determine SharePoint root URL from any method");
                throw new InvalidOperationException("Unable to determine SharePoint root URL for tenant");
            }

            _logger.LogInformation("✅ Organization info retrieval completed - TenantId: {TenantId}, SharePointUrl: {SharePointUrl}", 
                tenantId, sharePointRootUrl);

            return (tenantId, sharePointRootUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ HTTP error calling Graph API organization endpoint");
            _logger.LogError("🔍 HTTP exception details - Message: {Message}, Data: {Data}", ex.Message, ex.Data);
            throw new InvalidOperationException("Failed to call Graph API organization endpoint", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ JSON parsing error for organization response");
            _logger.LogError("🔍 JSON exception details - Path: {Path}, LineNumber: {LineNumber}", ex.Path, ex.LineNumber);
            throw new InvalidOperationException("Failed to parse organization response JSON", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to retrieve tenant information from Graph API - Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("🔍 Full exception details: {ExceptionDetails}", ex.ToString());
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
            _logger.LogInformation("🔄 Acquiring SharePoint access token for tenant: {TenantId}", tenantInfo.TenantId);
            _logger.LogInformation("🔍 SharePoint authentication context - TenantId: {TenantId}, SharePointUrl: {SharePointUrl}", 
                tenantInfo.TenantId, tenantInfo.SharePointRootUrl);
            
            // Extract the SharePoint host from the root URL
            var uri = new Uri(tenantInfo.SharePointRootUrl);
            _logger.LogInformation("🌐 SharePoint URI analysis - Host: {Host}, Scheme: {Scheme}, Full: {FullUri}", 
                uri.Host, uri.Scheme, uri.ToString());
            
            // Use the original working scope format for SharePoint REST API
            var scopes = new[] { $"{uri.Scheme}://{uri.Host}/.default" };
            _logger.LogInformation("📋 Requested SharePoint scopes: {Scopes}", string.Join(", ", scopes));

            // Validate input token again
            _logger.LogInformation("🔍 Input token validation for SharePoint - Length: {TokenLength}, Is valid: {IsValid}", 
                userToken?.Length ?? 0, !string.IsNullOrWhiteSpace(userToken));

            // Create UserAssertion from the incoming token
            _logger.LogInformation("🔑 Creating UserAssertion for SharePoint token exchange...");
            var userAssertion = new UserAssertion(userToken);
            _logger.LogInformation("✅ UserAssertion created for SharePoint");

            // Log MSAL configuration for SharePoint request
            _logger.LogInformation("🔧 MSAL Configuration for SharePoint - Client ID: {ClientId}, Authority: {Authority}", 
                _clientApp.AppConfig.ClientId, _clientApp.Authority);

            // Use On-Behalf-Of flow to get SharePoint token
            _logger.LogInformation("🔄 Executing OBO flow for SharePoint with scopes: {Scopes}...", string.Join(", ", scopes));
            
            var tokenBuilder = _clientApp.AcquireTokenOnBehalfOf(scopes, userAssertion);
            
            // Add correlation ID for tracking
            var correlationId = Guid.NewGuid();
            tokenBuilder.WithCorrelationId(correlationId);
            _logger.LogInformation("🔗 SharePoint OBO request correlation ID: {CorrelationId}", correlationId);
            
            // Add extra query parameters for debugging
            var extraQueryParameters = new Dictionary<string, string>
            {
                { "resource", $"{uri.Scheme}://{uri.Host}" } // Legacy parameter that might help
            };
            tokenBuilder.WithExtraQueryParameters(extraQueryParameters);
            _logger.LogInformation("🔧 Added extra query parameters - Resource: {Resource}", $"{uri.Scheme}://{uri.Host}");

            _logger.LogInformation("⏳ Executing SharePoint token request...");
            var result = await tokenBuilder.ExecuteAsync();

            _logger.LogInformation("✅ Successfully acquired SharePoint token - Length: {TokenLength}, Expires: {ExpiresOn}, Source: {Source}, Scopes: {Scopes}", 
                result.AccessToken?.Length ?? 0, result.ExpiresOn, result.AuthenticationResultMetadata?.TokenSource, string.Join(", ", result.Scopes ?? new string[0]));

            // Log account information for SharePoint token
            if (result.Account != null)
            {
                _logger.LogInformation("👤 SharePoint token account info - Username: {Username}, Environment: {Environment}", 
                    result.Account.Username, result.Account.Environment);
            }

            // Validate the returned token format
            if (!string.IsNullOrWhiteSpace(result.AccessToken))
            {
                var spTokenParts = result.AccessToken.Split('.');
                _logger.LogInformation("🔍 SharePoint token validation - Parts count: {PartsCount}, Valid JWT structure: {IsValidJWT}", 
                    spTokenParts.Length, spTokenParts.Length == 3);
            }

            return result.AccessToken;
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogError("❌ SharePoint token acquisition requires UI interaction - Error: {ErrorCode}", ex.ErrorCode);
            
            throw new UnauthorizedAccessException(
                $"SharePoint access requires additional permissions. Please ensure the Azure AD application has been granted the following permissions and admin consent has been provided: " +
                $"Sites.Read.All, Sites.Search.All, AllSites.Read. " +
                $"Required scope: {new Uri(tenantInfo.SharePointRootUrl).Scheme}://{new Uri(tenantInfo.SharePointRootUrl).Host}/.default. " +
                $"Error: {ex.ErrorCode} - {ex.Message}", ex);
        }
        catch (Microsoft.Identity.Client.MsalServiceException ex)
        {
            _logger.LogError("❌ MSAL service error during SharePoint token acquisition - Error: {ErrorCode}", ex.ErrorCode);
            throw new UnauthorizedAccessException("Failed to access SharePoint due to service error. Please try signing in again.", ex);
        }
        catch (Microsoft.Identity.Client.MsalClientException ex)
        {
            _logger.LogError("❌ MSAL client error during SharePoint token acquisition - Error: {ErrorCode}", ex.ErrorCode);
            throw new UnauthorizedAccessException("SharePoint client configuration error. Please check application registration.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to acquire SharePoint access token - Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("🔍 Full SharePoint token exception details: {ExceptionDetails}", ex.ToString());
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

    private async Task<object> ExecuteSearchAsync(string accessToken, SharePointSearchRequest request, TenantInfo tenantInfo)
    {
        var searchQuery = BuildSearchQuery(request);
        var searchUrl = BuildSearchUrl(searchQuery, request, tenantInfo);
        
        _logger.LogInformation("🔍 === EXECUTING SHAREPOINT SEARCH ===");
        _logger.LogInformation("📋 Search Parameters:");
        _logger.LogInformation("   Built Query: '{Query}'", searchQuery);
        _logger.LogInformation("   Max Results: {MaxResults}", request.MaxResults);
        _logger.LogInformation("   SharePoint Root URL: {SharePointUrl}", tenantInfo.SharePointRootUrl);
        _logger.LogInformation("   Full Search URL: {SearchUrl}", searchUrl);
        _logger.LogInformation("   Access Token Length: {TokenLength} chars", accessToken?.Length ?? 0);
        
        if (accessToken != null && accessToken.Length > 20)
        {
            var tokenPreview = $"{accessToken[..10]}...{accessToken[^10..]}";
            _logger.LogInformation("   Access Token Preview: {TokenPreview}", tokenPreview);
        }
        
        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        httpRequestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        httpRequestMessage.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        httpRequestMessage.Headers.Add("User-Agent", "SemanticKernelPoc/1.0");
        
        _logger.LogInformation("📤 HTTP REQUEST DETAILS:");
        _logger.LogInformation("   Method: {Method}", httpRequestMessage.Method);
        _logger.LogInformation("   URL: {Url}", httpRequestMessage.RequestUri);
        _logger.LogInformation("   Headers: {Headers}", string.Join(", ", httpRequestMessage.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
        
        string responseContent = string.Empty; // Declare outside try block for exception handlers
        var requestStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("📤 Sending SharePoint REST API request...");
            var response = await _httpClient.SendAsync(httpRequestMessage);
            requestStopwatch.Stop();
            
            responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("📥 === SHAREPOINT API RESPONSE ===");
            _logger.LogInformation("   Status Code: {StatusCode} ({StatusCodeInt})", response.StatusCode, (int)response.StatusCode);
            _logger.LogInformation("   Response Time: {ElapsedMs}ms", requestStopwatch.ElapsedMilliseconds);
            _logger.LogInformation("   Content Length: {ContentLength} characters", responseContent?.Length ?? 0);
            _logger.LogInformation("   Content Type: {ContentType}", response.Content.Headers.ContentType?.ToString() ?? "unknown");
            
            // Log response headers
            _logger.LogInformation("   Response Headers:");
            foreach (var header in response.Headers)
            {
                _logger.LogInformation("     {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
            }
            
            // *** LOG THE FULL RAW JSON RESPONSE ***
            if (!string.IsNullOrEmpty(responseContent))
            {
                if (responseContent.Length <= 2000)
                {
                    _logger.LogInformation("📋 === FULL SHAREPOINT API RESPONSE === {RawResponse}", responseContent);
                }
                else
                {
                    _logger.LogInformation("📋 === SHAREPOINT API RESPONSE (first 2000 chars) === {RawResponsePreview}...", responseContent[..2000]);
                    _logger.LogInformation("📋 === SHAREPOINT API RESPONSE (last 500 chars) === ...{RawResponseEnd}", responseContent[^500..]);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ SharePoint API returned empty response content");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ === SHAREPOINT SEARCH FAILED ===");
                _logger.LogError("   Status Code: {StatusCode}", response.StatusCode);
                _logger.LogError("   Reason Phrase: {ReasonPhrase}", response.ReasonPhrase);
                _logger.LogError("   Error Content: {Content}", responseContent);
                
                // Try to parse error details if JSON
                if (responseContent.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        var errorDoc = JsonDocument.Parse(responseContent);
                        if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            if (errorElement.TryGetProperty("message", out var messageElement))
                            {
                                _logger.LogError("   Error Message: {ErrorMessage}", messageElement.GetString());
                            }
                            if (errorElement.TryGetProperty("code", out var codeElement))
                            {
                                _logger.LogError("   Error Code: {ErrorCode}", codeElement.GetString());
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning("⚠️ Failed to parse error response as JSON: {ParseError}", parseEx.Message);
                    }
                }
                    
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError("🔒 Authentication/Authorization issue detected");
                    throw new UnauthorizedAccessException($"SharePoint Search access denied. Status: {response.StatusCode}. Check permissions and token scopes. Response: {responseContent}");
                }
                
                throw new InvalidOperationException($"SharePoint Search request failed: {response.StatusCode} - {responseContent}");
            }
            
            _logger.LogInformation("🔄 Deserializing SharePoint response JSON...");
            object searchResults;
            try
            {
                searchResults = JsonSerializer.Deserialize<object>(responseContent);
                _logger.LogInformation("✅ SharePoint search completed successfully - JSON deserialized");
                
                // Try to extract basic info about the results
                if (searchResults is JsonElement jsonElement)
                {
                    _logger.LogInformation("📊 === PARSED RESPONSE ANALYSIS ===");
                    _logger.LogInformation("   JSON Element Kind: {ValueKind}", jsonElement.ValueKind);
                    
                    if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        var properties = jsonElement.EnumerateObject().Select(p => p.Name).ToList();
                        _logger.LogInformation("   Top-level properties: [{Properties}]", string.Join(", ", properties));
                        
                        // Look for specific SharePoint search response properties
                        if (jsonElement.TryGetProperty("PrimaryQueryResult", out var primaryResult))
                        {
                            _logger.LogInformation("   Found PrimaryQueryResult");
                            if (primaryResult.TryGetProperty("RelevantResults", out var relevantResults))
                            {
                                _logger.LogInformation("   Found RelevantResults");
                                if (relevantResults.TryGetProperty("Table", out var table))
                                {
                                    _logger.LogInformation("   Found Table");
                                    if (table.TryGetProperty("Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                                    {
                                        _logger.LogInformation("   Found Rows array with {RowCount} items", rows.GetArrayLength());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "❌ JSON parsing error for SharePoint response");
                _logger.LogError("   JSON Error Position: {Position}", jsonEx.BytePositionInLine);
                _logger.LogError("   JSON Error Path: {Path}", jsonEx.Path ?? "(unknown)");
                _logger.LogError("   Response content that failed to parse: {ResponseContent}", responseContent);
                throw new InvalidOperationException($"Failed to parse SharePoint JSON response: {jsonEx.Message}", jsonEx);
            }
            
            return searchResults;
        }
        catch (HttpRequestException httpEx)
        {
            requestStopwatch.Stop();
            _logger.LogError(httpEx, "❌ HTTP request error during SharePoint search");
            _logger.LogError("   Request Time: {ElapsedMs}ms", requestStopwatch.ElapsedMilliseconds);
            _logger.LogError("   HTTP Error Message: {Message}", httpEx.Message);
            throw new InvalidOperationException($"HTTP error executing SharePoint search: {httpEx.Message}", httpEx);
        }
        catch (TaskCanceledException timeoutEx)
        {
            requestStopwatch.Stop();
            _logger.LogError(timeoutEx, "❌ Request timeout during SharePoint search");
            _logger.LogError("   Request Time: {ElapsedMs}ms", requestStopwatch.ElapsedMilliseconds);
            throw new InvalidOperationException($"SharePoint search request timed out after {requestStopwatch.ElapsedMilliseconds}ms", timeoutEx);
        }
        catch (Exception ex)
        {
            requestStopwatch.Stop();
            _logger.LogError(ex, "❌ Unexpected error executing SharePoint search request");
            _logger.LogError("   Request Time: {ElapsedMs}ms", requestStopwatch.ElapsedMilliseconds);
            _logger.LogError("   Exception Type: {ExceptionType}", ex.GetType().Name);
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

    private SharePointSearchResponse ParseSearchResults(object searchResults)
    {
        var response = new SharePointSearchResponse();

        try
        {
            _logger.LogInformation("🔍 Starting SharePoint search results parsing...");
            
            JsonElement jsonElement;
            if (searchResults is JsonElement element)
            {
                jsonElement = element;
                _logger.LogInformation("✅ Search results already in JsonElement format");
            }
            else
            {
                _logger.LogInformation("🔄 Converting search results to JsonElement...");
                var jsonString = JsonSerializer.Serialize(searchResults);
                _logger.LogInformation("📋 Serialized JSON for parsing: {JsonString}", jsonString);
                jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                _logger.LogInformation("✅ Converted to JsonElement successfully");
            }
            
            // Log the root structure for analysis
            _logger.LogInformation("🔍 Root JSON structure analysis:");
            _logger.LogInformation("   - Root value kind: {ValueKind}", jsonElement.ValueKind);
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var rootProperties = jsonElement.EnumerateObject().Select(p => p.Name).ToList();
                _logger.LogInformation("   - Root properties: [{Properties}]", string.Join(", ", rootProperties));
            }
            
            // SharePoint REST API format: direct access to PrimaryQueryResult
            if (!jsonElement.TryGetProperty("PrimaryQueryResult", out JsonElement primaryQueryResult))
            {
                _logger.LogWarning("❌ No PrimaryQueryResult found in SharePoint search response");
                _logger.LogWarning("🔍 Available root properties: [{Properties}]", 
                    string.Join(", ", jsonElement.EnumerateObject().Select(p => p.Name)));
                return response;
            }
            _logger.LogInformation("✅ Found PrimaryQueryResult");

            if (!primaryQueryResult.TryGetProperty("RelevantResults", out JsonElement relevantResults))
            {
                _logger.LogWarning("❌ No RelevantResults found in search response");
                _logger.LogWarning("🔍 Available PrimaryQueryResult properties: [{Properties}]", 
                    string.Join(", ", primaryQueryResult.EnumerateObject().Select(p => p.Name)));
                return response;
            }
            _logger.LogInformation("✅ Found RelevantResults");

            // Log RelevantResults structure
            var relevantResultsProps = relevantResults.EnumerateObject().Select(p => p.Name).ToList();
            _logger.LogInformation("🔍 RelevantResults properties: [{Properties}]", string.Join(", ", relevantResultsProps));

            if (!relevantResults.TryGetProperty("Table", out JsonElement table))
            {
                _logger.LogWarning("❌ No Table found in search response");
                _logger.LogWarning("🔍 Available RelevantResults properties: [{Properties}]", 
                    string.Join(", ", relevantResults.EnumerateObject().Select(p => p.Name)));
                if (relevantResults.TryGetProperty("Rows", out var rowsElement))
                {
                    _logger.LogWarning("🔍 Rows element type: {ValueKind}", rowsElement.ValueKind);
                }
                return response;
            }

            var rowCount = table.GetArrayLength();
            _logger.LogInformation("✅ Found Rows array with {RowCount} items", rowCount);

            // Parse each result row
            for (int i = 0; i < rowCount; i++)
            {
                var row = table[i];
                _logger.LogInformation("🔄 Processing row {RowIndex} of {TotalRows}", i + 1, rowCount);
                
                try
                {
                    var site = ParseSiteFromRow(row);
                    if (site != null && !string.IsNullOrEmpty(site.Url))
                    {
                        response.Sites.Add(site);
                        _logger.LogInformation("✅ Added site: {SiteTitle} - {SiteUrl}", site.Title, site.Url);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Skipped row {RowIndex} - site is null or has empty URL", i + 1);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "❌ Error parsing row {RowIndex}: {ErrorMessage}", i + 1, ex.Message);
                    continue;
                }
            }

            // Set total results count
            response.TotalResults = relevantResults.TryGetProperty("TotalRows", out JsonElement totalRowsElement) &&
                                    totalRowsElement.TryGetInt32(out int totalRows)
                                    ? totalRows
                                    : response.Sites.Count;
            
            _logger.LogInformation("📊 Parsing complete - TotalRows from API: {ApiTotal}, Parsed sites: {ParsedCount}", 
                response.TotalResults, response.Sites.Count);
            _logger.LogInformation("✅ Successfully parsed {SiteCount} sites from SharePoint search results", response.Sites.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing SharePoint search results - Exception: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("🔍 Full parsing exception: {ExceptionDetails}", ex.ToString());
            throw new InvalidOperationException($"Failed to parse SharePoint search results: {ex.Message}", ex);
        }

        return response;
    }

    private SharePointSite ParseSiteFromRow(JsonElement row)
    {
        var site = new SharePointSite();
        
        _logger.LogInformation("🔍 Parsing site from row...");
        
        // SharePoint format: Cells is a direct array
        if (!row.TryGetProperty("Cells", out JsonElement cellsArray) || cellsArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("❌ No Cells array found in row or wrong type");
            if (row.TryGetProperty("Cells", out var cellsElement))
            {
                _logger.LogWarning("🔍 Cells element type: {ValueKind}", cellsElement.ValueKind);
            }
            
            // Log available row properties for debugging
            var rowProps = row.EnumerateObject().Select(p => p.Name).ToList();
            _logger.LogWarning("🔍 Available row properties: [{Properties}]", string.Join(", ", rowProps));
            return site;
        }

        var cellCount = cellsArray.GetArrayLength();
        _logger.LogInformation("📋 Found {CellCount} cells in row", cellCount);

        // Parse each cell key-value pair
        for (int i = 0; i < cellCount; i++)
        {
            var cell = cellsArray[i];
            
            if (!cell.TryGetProperty("Key", out JsonElement keyElement) ||
                !cell.TryGetProperty("Value", out JsonElement valueElement))
            {
                _logger.LogWarning("⚠️ Cell {CellIndex} missing Key or Value property", i);
                continue;
            }

            var key = keyElement.GetString();
            var value = valueElement.ValueKind == JsonValueKind.Null ? null : valueElement.GetString();

            if (string.IsNullOrEmpty(key)) 
            {
                _logger.LogWarning("⚠️ Cell {CellIndex} has empty or null key", i);
                continue;
            }

            _logger.LogInformation("🔑 Processing cell {CellIndex}: {Key} = {Value}", i, key, value ?? "null");

            switch (key)
            {
                case "Title":
                    site.Title = value ?? "";
                    _logger.LogInformation("   ✅ Set Title: {Title}", site.Title);
                    break;
                case "Path":
                    site.Url = value ?? "";
                    _logger.LogInformation("   ✅ Set URL: {Url}", site.Url);
                    break;
                case "Description":
                    site.Description = value ?? "";
                    _logger.LogInformation("   ✅ Set Description: {Description}", 
                        string.IsNullOrEmpty(site.Description) ? "(empty)" : 
                        site.Description.Length > 50 ? site.Description.Substring(0, 50) + "..." : site.Description);
                    break;
                case "Created":
                    if (DateTime.TryParse(value, out DateTime createdDate))
                    {
                        site.Created = createdDate;
                        _logger.LogInformation("   ✅ Set Created: {Created}", site.Created);
                    }
                    else
                    {
                        _logger.LogWarning("   ⚠️ Failed to parse Created date: {Value}", value);
                    }
                    break;
                case "LastModifiedTime":
                    if (DateTime.TryParse(value, out DateTime modifiedDate))
                    {
                        site.LastModified = modifiedDate;
                        _logger.LogInformation("   ✅ Set LastModified: {LastModified}", site.LastModified);
                    }
                    else
                    {
                        _logger.LogWarning("   ⚠️ Failed to parse LastModifiedTime: {Value}", value);
                    }
                    break;
                case "WebTemplate":
                    site.WebTemplate = value ?? "";
                    _logger.LogInformation("   ✅ Set WebTemplate: {WebTemplate}", site.WebTemplate);
                    break;
                case "Rank":
                    if (double.TryParse(value, out double rank))
                    {
                        site.Relevance = rank;
                        _logger.LogInformation("   ✅ Set Relevance: {Relevance}", site.Relevance);
                    }
                    else
                    {
                        _logger.LogWarning("   ⚠️ Failed to parse Rank: {Value}", value);
                    }
                    break;
                default:
                    _logger.LogInformation("   ℹ️ Unhandled property: {Key} = {Value}", key, value);
                    break;
            }
        }
        
        _logger.LogInformation("📋 Site parsing summary - Title: {Title}, URL: {Url}, Created: {Created}", 
            site.Title, site.Url, site.Created);

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