namespace SemanticKernelPoc.McpServer.Services;

public class SharePointSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string CreatedAfter { get; set; } = string.Empty;
    public string CreatedBefore { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 50;
    
    // Enhanced parameters for intelligent search
    public string TimePeriod { get; set; } = string.Empty; // "last_week", "this_month", "last_3_days", etc.
    public string SortBy { get; set; } = string.Empty; // "created", "modified", "title", "relevance"
    public string SortOrder { get; set; } = "desc"; // "asc" or "desc"
    public string SearchScope { get; set; } = string.Empty; // "title", "description", "all", "title_and_description"
    public bool ExactMatch { get; set; } = false; // Whether to search for exact phrase
    public List<string> Keywords { get; set; } = new List<string>(); // Individual keywords
    public string Template { get; set; } = string.Empty; // Filter by specific SharePoint template
}

public class SharePointSearchResponse
{
    public List<SharePointSite> Sites { get; set; } = new List<SharePointSite>();
    public int TotalResults { get; set; }
    public bool HasMore { get; set; }
    public string SearchQuery { get; set; } = string.Empty; // The actual query that was executed
    public string QueryExplanation { get; set; } = string.Empty; // Human-readable explanation of what was searched
}

public class SharePointSite
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string WebTemplate { get; set; } = string.Empty;
    public double Relevance { get; set; } = 0.0; // Search relevance score
}