using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;

namespace SemanticKernelPoc.Api.Services;

public interface IMcpClientService
{
    Task<string> CallMcpFunctionAsync(string functionName, Dictionary<string, object> parameters);
}

public class McpClientService : IMcpClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClientService> _logger;
    private readonly string _mcpServerUrl;

    public McpClientService(HttpClient httpClient, ILogger<McpClientService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _mcpServerUrl = configuration["McpServer:BaseUrl"] ?? "https://localhost:31339";
        
        // Configure default headers for MCP communication
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    public async Task<string> CallMcpFunctionAsync(string functionName, Dictionary<string, object> parameters)
    {
        try
        {
            _logger.LogInformation("Calling MCP function: {FunctionName} with parameters: {Parameters}",
                functionName, JsonSerializer.Serialize(parameters));

            // Create MCP message for tools/call
            var mcpMessage = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = functionName,
                    arguments = parameters
                }
            };

            var jsonContent = JsonSerializer.Serialize(mcpMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Use the root endpoint for SSE MCP communication
            var response = await _httpClient.PostAsync(_mcpServerUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("MCP server returned error {StatusCode}: {ErrorContent}",
                    response.StatusCode, errorContent);
                return $"MCP server error ({response.StatusCode}): {errorContent}";
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("MCP function {FunctionName} completed successfully", functionName);

            // Parse SSE response format: "event: message\ndata: {json}\n\n"
            var sseData = responseContent;
            if (sseData.StartsWith("event: message"))
            {
                var lines = sseData.Split('\n');
                var dataLine = lines.FirstOrDefault(line => line.StartsWith("data: "));
                if (dataLine != null)
                {
                    sseData = dataLine.Substring(6); // Remove "data: " prefix
                }
            }

            // Parse JSON-RPC response and extract the result
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(sseData);
            if (jsonResponse.TryGetProperty("result", out var result))
            {
                if (result.TryGetProperty("content", out var content_prop))
                {
                    return content_prop.ToString();
                }
                return result.ToString();
            }

            return sseData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling MCP function {FunctionName}", functionName);
            return $"Network error: {ex.Message}. Please check if the MCP server is running.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP function {FunctionName}", functionName);
            return $"Error calling MCP function: {ex.Message}";
        }
    }
} 