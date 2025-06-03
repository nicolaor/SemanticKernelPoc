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

        // Configure comprehensive logging for web server
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        // Add our SharePoint search service
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<ISharePointSearchService, SharePointSearchService>();
        
        // Configure MCP server with HTTP/SSE transport for multi-client support
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();

        // Add debug logging to see what tools are registered
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🔧 === MCP SERVER STARTING - CHECKING TOOL REGISTRATION ===");

        // Add middleware to log all HTTP requests
        app.Use(async (context, next) =>
        {
            logger.LogInformation("🌐 === INCOMING HTTP REQUEST ===");
            logger.LogInformation("🌐 Method: {Method}", context.Request.Method);
            logger.LogInformation("🌐 Path: {Path}", context.Request.Path);
            logger.LogInformation("🌐 Query: {Query}", context.Request.QueryString);
            logger.LogInformation("🌐 Headers: {Headers}", string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}:{h.Value}")));
            
            if (context.Request.ContentLength > 0)
            {
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;
                logger.LogInformation("🌐 Body: {Body}", body);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            await next();
            
            stopwatch.Stop();
            logger.LogInformation("🌐 === HTTP REQUEST COMPLETED in {ElapsedMs}ms ===", stopwatch.ElapsedMilliseconds);
            logger.LogInformation("🌐 Response Status: {StatusCode}", context.Response.StatusCode);
        });
        
        // Map MCP endpoints for HTTP/SSE transport
        app.MapMcp();

        // Add health check endpoint
        app.MapGet("/health", () => "Healthy");

        logger.LogInformation("🚀 === MCP SERVER CONFIGURED AND STARTING ===");

        await app.RunAsync();
    }
}