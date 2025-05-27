# SharePoint Search MCP Server

This is a Model Context Protocol (MCP) server that provides SharePoint search functionality specifically designed to find CoffeeNet (CN365) sites. The server integrates with SharePoint's Search REST API to locate sites that have the `CN365TemplateId` property set.

## Features

- **CoffeeNet Site Search**: Searches for SharePoint sites with the `CN365TemplateId` property
- **Date Range Filtering**: Filter sites by creation date range
- **Text Search**: Optional text-based search within site titles and descriptions
- **MCP Protocol Compliance**: Fully compatible with the Model Context Protocol specification

## Prerequisites

- .NET 8.0 or later
- Azure AD application registration with SharePoint permissions
- SharePoint tenant with indexed `CN365TemplateId` managed property

## Setup

### 1. Azure AD Application Registration

1. Register a new application in Azure AD
2. Grant the following permissions:
   - `Sites.Read.All` (Application permission)
   - `Sites.Search.All` (Application permission)
3. Create a client secret
4. Note down the Tenant ID, Client ID, and Client Secret

### 2. Configuration

Update the `appsettings.json` file with your Azure AD and SharePoint details:

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

Alternatively, use user secrets for sensitive information:

```bash
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "SharePoint:TenantName" "your-tenant-name"
```

### 3. Build and Run

```bash
dotnet build
dotnet run
```

## MCP Tools

### search_coffeenet_sites

Searches for CoffeeNet sites in SharePoint using the CN365TemplateId property.

**Parameters:**
- `query` (optional): Text search query to filter sites
- `createdAfter` (optional): Filter for sites created after this date (ISO 8601 format)
- `createdBefore` (optional): Filter for sites created before this date (ISO 8601 format)
- `maxResults` (optional): Maximum number of results to return (default: 50, max: 500)

**Example Usage:**

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

## Integration with Semantic Kernel

To integrate this MCP server with your Semantic Kernel application:

1. Start the MCP server
2. Configure your Semantic Kernel to connect to the MCP server
3. Use the `search_coffeenet_sites` tool in your AI workflows

## SharePoint Search Query Details

The server constructs SharePoint search queries with the following logic:

- Base query: `contentclass:STS_Site AND CN365TemplateIdOWSText:*`
- Adds text search if provided: `AND (your-search-text)`
- Adds date filters if provided: `AND Created>=date AND Created<=date`

## Troubleshooting

### Common Issues

1. **Authentication Errors**: Ensure your Azure AD app has the correct permissions and the client secret is valid
2. **No Results**: Verify that the `CN365TemplateId` property is indexed as a managed property in SharePoint
3. **Permission Denied**: Check that the application has `Sites.Read.All` and `Sites.Search.All` permissions

### Logging

The server uses structured logging. Set the log level in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## Development

### Project Structure

```
SemanticKernelPoc.McpServer/
├── Models/
│   └── McpModels.cs          # MCP protocol and SharePoint models
├── Services/
│   ├── McpServer.cs          # Main MCP server implementation
│   └── SharePointSearchService.cs # SharePoint search logic
├── Program.cs                # Application entry point
├── appsettings.json         # Configuration
└── README.md               # This file
```

### Adding New Tools

To add new MCP tools:

1. Define the tool in `HandleToolsList()` method
2. Add a handler method in `HandleToolCallAsync()`
3. Implement the business logic in the appropriate service

## License

This project is part of the SemanticKernelPoc solution. 