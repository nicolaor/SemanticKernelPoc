using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SemanticKernelPoc.McpServer.Services;

namespace SemanticKernelPoc.McpServer;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure logging for web server
        builder.Logging.AddConsole();

        // Add our SharePoint search service
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<ISharePointSearchService, SharePointSearchService>();

        // Configure MCP server with HTTP/SSE transport for multi-client support
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();

        // Map MCP endpoints for HTTP/SSE transport
        app.MapMcp();

        // Add health check endpoint
        app.MapGet("/health", () => "Healthy");

        await app.RunAsync();
    }
}