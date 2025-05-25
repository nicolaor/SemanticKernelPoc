using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.SharePoint;

public class SharePointPlugin : BaseGraphPlugin
{
    public SharePointPlugin(IGraphService graphService, ILogger<SharePointPlugin> logger) 
        : base(graphService, logger)
    {
    }

    [KernelFunction, Description("Get the user's SharePoint sites")]
    public async Task<string> GetUserSites(Kernel kernel)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var sites = await graphClient.Sites.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Search = "*";
                    requestConfig.QueryParameters.Top = 10;
                });

                if (sites?.Value?.Any() == true)
                {
                    var siteList = sites.Value.Select(site => new
                    {
                        Name = site.Name,
                        Url = site.WebUrl,
                        Description = site.Description
                    });

                    return FormatJsonResponse(siteList, userName, "SharePoint sites", sites.Value.Count);
                }

                return $"No SharePoint sites found for {userName}.";
            },
            "GetUserSites"
        );
    }

    [KernelFunction, Description("Search for files in the user's SharePoint sites")]
    public async Task<string> SearchFiles(Kernel kernel, [Description("Search query for files")] string query)
    {
        var validation = ValidateRequiredParameter(query, "Search query");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var sites = await graphClient.Sites.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Search = "*";
                    requestConfig.QueryParameters.Top = 5;
                });

                if (sites?.Value?.Any() == true)
                {
                    var searchResults = new List<object>();
                    foreach (var site in sites.Value.Take(3))
                    {
                        searchResults.Add(new
                        {
                            SiteName = site.Name,
                            SiteUrl = site.WebUrl,
                            SearchQuery = query,
                            Note = "File search requires advanced SharePoint Search API implementation"
                        });
                    }

                    return FormatJsonResponse(searchResults, userName, $"SharePoint file search results for '{query}'");
                }

                return $"No SharePoint sites accessible for file search by {userName}.";
            },
            "SearchFiles"
        );
    }

    [KernelFunction, Description("Browse SharePoint site contents")]
    public async Task<string> BrowseSiteContents(Kernel kernel, [Description("Site name or URL to browse")] string siteIdentifier)
    {
        var validation = ValidateRequiredParameter(siteIdentifier, "Site identifier");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var sites = await graphClient.Sites.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Search = siteIdentifier;
                    requestConfig.QueryParameters.Top = 1;
                });

                if (sites?.Value?.Any() != true)
                {
                    return $"Site '{siteIdentifier}' not found for {userName}.";
                }

                var site = sites.Value.First();
                
                return CreateSuccessResponse(
                    "SharePoint site access",
                    userName,
                    ("📁 Site Name", site.Name ?? "Unknown"),
                    ("🌐 Site URL", site.WebUrl ?? "Unknown"),
                    ("📝 Note", "Detailed folder browsing requires Microsoft Graph Drive API configuration")
                );
            },
            "BrowseSiteContents"
        );
    }

    [KernelFunction, Description("Get basic information about SharePoint files (read-only)")]
    public async Task<string> GetFileInfo(Kernel kernel, 
        [Description("SharePoint site name to search in")] string siteName,
        [Description("File name to search for")] string fileName)
    {
        var validation = ValidateRequiredParameter(fileName, "File name");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var sites = await graphClient.Sites.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Search = siteName;
                    requestConfig.QueryParameters.Top = 1;
                });

                if (sites?.Value?.Any() != true)
                {
                    return $"Site '{siteName}' not found for {userName}.";
                }

                var site = sites.Value.First();

                return CreateSuccessResponse(
                    "SharePoint file search",
                    userName,
                    ("📁 Site", site.Name ?? "Unknown"),
                    ("📄 File Search", fileName),
                    ("📝 Note", "Detailed file information requires advanced Microsoft Graph Search API implementation")
                );
            },
            "GetFileInfo"
        );
    }

    [KernelFunction, Description("Get detailed information about a SharePoint site")]
    public async Task<string> GetSiteDetails(Kernel kernel, [Description("Site URL or site ID")] string siteIdentifier)
    {
        var validation = ValidateRequiredParameter(siteIdentifier, "Site identifier");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                if (siteIdentifier.StartsWith("http"))
                {
                    return CreateSuccessResponse(
                        "SharePoint site URL access",
                        userName,
                        ("🌐 Requested URL", siteIdentifier),
                        ("📝 Note", "Site access by URL requires advanced configuration. Please use site ID or GetUserSites to find available sites")
                    );
                }
                
                var site = await graphClient.Sites[siteIdentifier].GetAsync();

                if (site != null)
                {
                    var siteDetails = new
                    {
                        Name = site.DisplayName,
                        Description = site.Description,
                        WebUrl = site.WebUrl,
                        SiteId = site.Id,
                        CreatedDateTime = site.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                        LastModifiedDateTime = site.LastModifiedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"
                    };

                    return FormatJsonResponse(new[] { siteDetails }, userName, "SharePoint site details");
                }

                return $"SharePoint site not found or accessible for {userName}: {siteIdentifier}";
            },
            "GetSiteDetails"
        );
    }

    [KernelFunction, Description("Get document libraries in a SharePoint site")]
    public async Task<string> GetSiteLibraries(Kernel kernel, [Description("Site ID")] string siteId)
    {
        var validation = ValidateRequiredParameter(siteId, "Site ID");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var site = await graphClient.Sites[siteId].GetAsync();
                
                if (site != null)
                {
                    return CreateSuccessResponse(
                        "SharePoint site library access",
                        userName,
                        ("📁 Site", site.DisplayName ?? "Unknown"),
                        ("🌐 URL", site.WebUrl ?? "Unknown"),
                        ("📝 Note", "Document library enumeration requires advanced Microsoft Graph Drive API configuration")
                    );
                }

                return $"SharePoint site with ID '{siteId}' not found for {userName}.";
            },
            "GetSiteLibraries"
        );
    }

    [KernelFunction, Description("Browse files in a SharePoint document library")]
    public async Task<string> BrowseLibraryFiles(Kernel kernel, 
        [Description("Site ID")] string siteId,
        [Description("Drive/Library ID")] string driveId,
        [Description("Folder path (optional, use 'root' for root folder)")] string folderPath = "root")
    {
        var siteValidation = ValidateRequiredParameter(siteId, "Site ID");
        if (siteValidation != null) return siteValidation;

        var driveValidation = ValidateRequiredParameter(driveId, "Drive ID");
        if (driveValidation != null) return driveValidation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var site = await graphClient.Sites[siteId].GetAsync();
                var drive = await graphClient.Sites[siteId].Drives[driveId].GetAsync();

                if (site != null && drive != null)
                {
                    return CreateSuccessResponse(
                        "SharePoint library file browsing",
                        userName,
                        ("📁 Site", site.DisplayName ?? "Unknown"),
                        ("💾 Library", drive.Name ?? "Unknown"),
                        ("📂 Path", folderPath),
                        ("📝 Note", "File browsing functionality requires advanced Microsoft Graph Drive Items API configuration")
                    );
                }

                return $"Could not access the specified SharePoint library for {userName}.";
            },
            "BrowseLibraryFiles"
        );
    }
} 