# SharePoint MCP Server Integration

This document describes the new Model Context Protocol (MCP) server integration that enables SharePoint search functionality for CoffeeNet (CN365) sites within the Semantic Kernel POC application.

## Overview

The integration consists of two main components:

1. **SemanticKernelPoc.McpServer** - A standalone MCP server that handles SharePoint search operations
2. **MCP Client Integration** - Client-side integration in the API project that communicates with the MCP server

## Architecture

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│   Semantic Kernel   │    │    MCP Client        │    │    MCP Server       │
│   (AI Assistant)    │◄──►│   (API Project)      │◄──►│  (SharePoint Search)│
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
                                      │                           │
                                      │                           │
                                      ▼                           ▼
                           ┌──────────────────────┐    ┌─────────────────────┐
                           │  SharePoint Plugin   │    │  SharePoint REST    │
                           │  (SK Functions)      │    │      API            │
                           └──────────────────────┘    └─────────────────────┘
```

## Features

### MCP Server Features
- **CoffeeNet Site Search**: Finds SharePoint sites with `CN365TemplateId` property
- **Date Range Filtering**: Filter sites by creation date
- **Text Search**: Search within site titles and descriptions
- **MCP Protocol Compliance**: Full MCP 2024-11-05 protocol support

### Semantic Kernel Integration
- **Native SK Functions**: SharePoint search exposed as SK functions
- **AI-Powered Queries**: Natural language queries processed by AI
- **Multiple Search Methods**: Various search strategies (recent, keyword-based, etc.)

## Setup Instructions

### 1. Azure AD Configuration

Create an Azure AD application with the following permissions:
- `Sites.Read.All` (Application permission)
- `Sites.Search.All` (Application permission)

### 2. SharePoint Configuration

Ensure the `CN365TemplateId` property is indexed as a managed property in SharePoint search.

### 3. MCP Server Configuration

Configure the MCP server in `SemanticKernelPoc.McpServer/appsettings.json`:

```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  },
  "SharePoint": {
    "TenantName": "your-tenant-name"
  }
}
```

Or use user secrets:

```bash
cd SemanticKernelPoc.McpServer
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "SharePoint:TenantName" "your-tenant-name"
```

### 4. API Project Configuration

The API project automatically discovers and starts the MCP server. Optionally configure the path in `appsettings.json`:

```json
{
  "McpServer": {
    "ExecutablePath": "path/to/SemanticKernelPoc.McpServer.exe"
  }
}
```

## Usage

### 1. Direct API Endpoints

#### Check MCP Server Status
```http
GET /api/sharepoint/status
```

#### Search CoffeeNet Sites
```http
GET /api/sharepoint/coffeenet-sites?query=project&maxResults=10
```

#### Search Recent Sites
```http
GET /api/sharepoint/coffeenet-sites/recent?daysBack=30
```

#### AI-Powered Search
```http
POST /api/sharepoint/ai-search
Content-Type: application/json

{
  "userQuery": "Find all project management sites created last month"
}
```

### 2. Semantic Kernel Functions

The following functions are automatically available in Semantic Kernel:

#### `SharePointSearch.search_coffeenet_sites`
```csharp
var result = await kernel.InvokeAsync("SharePointSearch", "search_coffeenet_sites", new()
{
    ["query"] = "project management",
    ["maxResults"] = 10
});
```

#### `SharePointSearch.search_recent_coffeenet_sites`
```csharp
var result = await kernel.InvokeAsync("SharePointSearch", "search_recent_coffeenet_sites", new()
{
    ["daysBack"] = 30
});
```

#### `SharePointSearch.find_coffeenet_sites_by_keyword`
```csharp
var result = await kernel.InvokeAsync("SharePointSearch", "find_coffeenet_sites_by_keyword", new()
{
    ["keywords"] = "collaboration workspace"
});
```

### 3. AI Assistant Integration

The AI assistant can automatically use these functions when users ask about SharePoint sites:

**User**: "Show me all CoffeeNet sites created in the last week"

**AI Response**: The AI will automatically call the appropriate function and return formatted results.

## MCP Protocol Details

### Supported Methods

- `initialize` - Initialize the MCP server
- `tools/list` - List available tools
- `tools/call` - Execute a tool

### Tool: search_coffeenet_sites

**Parameters:**
- `query` (optional): Text search query
- `createdAfter` (optional): ISO 8601 date string
- `createdBefore` (optional): ISO 8601 date string
- `maxResults` (optional): Integer (1-500, default: 50)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "search_coffeenet_sites",
    "arguments": {
      "query": "project management",
      "createdAfter": "2024-01-01T00:00:00Z",
      "maxResults": 10
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Found 3 CoffeeNet sites:\n\n**Project Alpha**\nURL: https://tenant.sharepoint.com/sites/project-alpha\nCreated: 2024-01-15\nCN365 Template ID: PROJ001\n\n---\n\n..."
      }
    ],
    "isError": false
  }
}
```

## Development

### Project Structure

```
SemanticKernelPoc.McpServer/
├── Models/
│   └── McpModels.cs              # MCP protocol models
├── Services/
│   ├── McpServer.cs              # Main MCP server
│   └── SharePointSearchService.cs # SharePoint integration
├── Program.cs                    # Entry point
├── appsettings.json             # Configuration
└── README.md                    # Server documentation

SemanticKernelPoc.Api/
├── Services/
│   └── McpClientService.cs      # MCP client
├── Plugins/
│   └── SharePointSearchPlugin.cs # SK plugin
└── Controllers/
    └── SharePointController.cs   # Test endpoints
```

### Adding New MCP Tools

1. **Define the tool** in `McpServer.HandleToolsList()`
2. **Add handler** in `McpServer.HandleToolCallAsync()`
3. **Implement logic** in `SharePointSearchService`
4. **Add SK function** in `SharePointSearchPlugin`

### Testing

#### Test MCP Server Directly
```bash
cd SemanticKernelPoc.McpServer
dotnet run
```

Then send JSON-RPC requests via stdin.

#### Test via API
```bash
cd SemanticKernelPoc.Api
dotnet run
```

Navigate to `/swagger` to test the endpoints.

## Troubleshooting

### Common Issues

1. **MCP Server Not Starting**
   - Check that the executable path is correct
   - Verify .NET 8.0 is installed
   - Check logs for startup errors

2. **Authentication Errors**
   - Verify Azure AD app permissions
   - Check client secret validity
   - Ensure tenant ID is correct

3. **No Search Results**
   - Verify `CN365TemplateId` is indexed in SharePoint
   - Check search permissions
   - Validate search query syntax

4. **Connection Issues**
   - Ensure MCP server process is running
   - Check for port conflicts
   - Verify JSON-RPC communication

### Logging

Enable debug logging in both projects:

**MCP Server** (`appsettings.Development.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

**API Project** (`appsettings.Development.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "SemanticKernelPoc.Api.Services.McpClientService": "Debug"
    }
  }
}
```

## Security Considerations

- Store sensitive configuration in user secrets or environment variables
- Use application permissions (not delegated) for service-to-service calls
- Implement proper error handling to avoid information disclosure
- Consider rate limiting for production deployments

## Performance Considerations

- MCP server process lifecycle management
- SharePoint search result caching
- Connection pooling for multiple requests
- Async/await patterns throughout

## Future Enhancements

- Support for additional SharePoint content types
- Caching layer for frequently accessed sites
- Batch operations for multiple searches
- Real-time notifications for site changes
- Integration with other Microsoft 365 services 