namespace SemanticKernelPoc.McpServer.Services;

public class SharePointSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string CreatedAfter { get; set; } = string.Empty;
    public string CreatedBefore { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 50;
}

public class SharePointSearchResponse
{
    public List<SharePointSite> Sites { get; set; } = new List<SharePointSite>();
    public int TotalResults { get; set; }
    public bool HasMore { get; set; }
}

public class SharePointSite
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string WebTemplate { get; set; } = string.Empty;
} 