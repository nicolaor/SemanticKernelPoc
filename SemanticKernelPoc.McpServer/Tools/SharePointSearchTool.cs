using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SemanticKernelPoc.McpServer.Services;
using System.ComponentModel;
using System.Text.Json;

namespace SemanticKernelPoc.McpServer.Tools;

[McpServerToolType]
public static class SharePointSearchTool
{
    [McpServerTool]
    [Description("Simple test with no dependencies to verify MCP execution")]
    public static string SimpleTest()
    {
        Console.WriteLine("🟢🟢🟢 SIMPLE TEST METHOD CALLED WITH NO DEPENDENCIES! 🟢🟢🟢");
        return "🟢 Simple static test method executed successfully - no dependencies";
    }

    [McpServerTool]
    [Description("Search for SharePoint sites - simplified version to test execution")]
    public static string SearchSharePointSites(
        [Description("User access token for authentication")] string accessToken,
        [Description("Text search query to filter sites")] string searchQuery = "",
        [Description("Maximum number of results to return")] int maxResults = 50)
    {
        Console.WriteLine("🔥🔥🔥 SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
        Console.WriteLine($"🔥 SearchSharePointSites called with accessToken length: {accessToken?.Length ?? 0}");
        Console.WriteLine($"🔥 SearchSharePointSites called with searchQuery: '{searchQuery}'");
        Console.WriteLine($"🔥 SearchSharePointSites called with maxResults: {maxResults}");
        
        if (string.IsNullOrEmpty(accessToken))
        {
            Console.WriteLine("🔥 RETURNING ERROR - NO TOKEN");
            return "❌ SharePoint Search Error: User access token is required for authentication.";
        }

        Console.WriteLine("🔥 RETURNING MOCK SHAREPOINT DATA");
        
        // Return mock SharePoint sites data in JSON format that the AI can understand
        var mockSites = new[]
        {
            new
            {
                title = "Project Alpha Team Site",
                url = "https://contoso.sharepoint.com/sites/project-alpha",
                description = "Main collaboration site for Project Alpha team members",
                created = "2024-01-15",
                webTemplate = "GROUP"
            },
            new
            {
                title = "Marketing Documents",
                url = "https://contoso.sharepoint.com/sites/marketing",
                description = "Central repository for marketing materials and campaigns",
                created = "2024-01-20",
                webTemplate = "STS"
            },
            new
            {
                title = "HR Team Channel",
                url = "https://contoso.sharepoint.com/sites/hr-team",
                description = "Human Resources team collaboration space",
                created = "2024-01-25",
                webTemplate = "TEAMCHANNEL"
            }
        };

        var response = new
        {
            sites = mockSites,
            totalResults = mockSites.Length,
            searchQuery = searchQuery,
            message = $"Found {mockSites.Length} SharePoint sites"
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Search for recent SharePoint sites - simplified version")]
    public static string SearchRecentSharePointSites(
        [Description("User access token for authentication")] string accessToken,
        [Description("Text search query to filter sites")] string searchQuery = "",
        [Description("Number of results to return")] int count = 3,
        [Description("Limit for results")] int limit = 3,
        [Description("Maximum number of results")] int maxResults = 3)
    {
        Console.WriteLine("🔥🔥🔥 RECENT SHAREPOINT SEARCH METHOD CALLED! 🔥🔥🔥");
        Console.WriteLine($"🔥 SearchRecentSharePointSites called with accessToken length: {accessToken?.Length ?? 0}");
        Console.WriteLine($"🔥 SearchRecentSharePointSites called with searchQuery: '{searchQuery}'");
        Console.WriteLine($"🔥 SearchRecentSharePointSites called with count: {count}");
        Console.WriteLine($"🔥 SearchRecentSharePointSites called with limit: {limit}");
        Console.WriteLine($"🔥 SearchRecentSharePointSites called with maxResults: {maxResults}");
        
        if (string.IsNullOrEmpty(accessToken))
        {
            Console.WriteLine("🔥 RETURNING ERROR - NO TOKEN");
            return "❌ Recent SharePoint Search Error: User access token is required for authentication.";
        }

        Console.WriteLine("🔥 RETURNING MOCK RECENT SHAREPOINT DATA");
        
        // Return recent SharePoint sites data in JSON format that the AI can understand
        var recentSites = new[]
        {
            new
            {
                title = "Q1 2024 Planning",
                url = "https://contoso.sharepoint.com/sites/q1-planning",
                description = "Quarterly planning and strategy discussions",
                created = "2024-01-30",
                webTemplate = "GROUP"
            },
            new
            {
                title = "Product Launch Team",
                url = "https://contoso.sharepoint.com/sites/product-launch",
                description = "Coordination site for upcoming product launch",
                created = "2024-01-28",
                webTemplate = "STS"
            },
            new
            {
                title = "Customer Success Hub",
                url = "https://contoso.sharepoint.com/sites/customer-success",
                description = "Customer success team collaboration and resources",
                created = "2024-01-26",
                webTemplate = "GROUP"
            }
        }.Take(count).ToArray();

        var response = new
        {
            sites = recentSites,
            totalResults = recentSites.Length,
            searchQuery = searchQuery,
            message = $"Found {recentSites.Length} recent SharePoint sites"
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Simple test method to verify MCP tool execution")]
    public static string TestMethod()
    {
        Console.WriteLine("🎯🎯🎯 TEST METHOD CALLED 🎯🎯🎯");
        Console.WriteLine("✅ TestMethod executed successfully");
        
        return "🎯 Test method executed successfully";
    }
}