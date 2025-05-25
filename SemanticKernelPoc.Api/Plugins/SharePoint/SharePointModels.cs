namespace SemanticKernelPoc.Api.Plugins.SharePoint;

public record SharePointSiteInfo(
    string Name,
    string Url,
    string Description
);

public record SharePointSiteDetails(
    string Name,
    string Description,
    string WebUrl,
    string SiteId,
    string CreatedDateTime,
    string LastModifiedDateTime
);

public record SharePointSearchResult(
    string SiteName,
    string SiteUrl,
    string SearchQuery,
    string Note
); 