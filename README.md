# 🤖 Semantic Kernel PoC - Microsoft 365 AI Assistant

A comprehensive proof-of-concept application demonstrating the integration of **Microsoft Semantic Kernel** with **Microsoft Graph API** and **Model Context Protocol (MCP)** to create an intelligent AI assistant for Microsoft 365 productivity tasks.

## 📋 Overview

This application provides a conversational AI interface that can interact with your Microsoft 365 data through natural language. The AI assistant can help you manage calendars, emails, SharePoint sites, OneDrive content, and tasks using Microsoft To Do, featuring structured output responses with dynamic card rendering and intelligent function calling.

## 🏗️ Architecture

### System Architecture
```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   React Client  │────▶│  ASP.NET Core   │────▶│   MCP Server    │
│   (Port 31337)  │     │   (Port 31338)  │     │   (Port 31339)  │
│                 │     │                 │     │                 │
│ • MSAL Auth     │     │ • Semantic K.   │     │ • SharePoint    │
│ • Chat UI       │     │ • Function Call │     │ • SSE Transport │
│ • TypeScript    │     │ • Plugins       │     │ • OAuth Token   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                       │                       │
         └───────────────────────▼───────────────────────┘
                       ┌─────────────────┐
                       │ Microsoft Graph │
                       │ • Mail/Calendar │
                       │ • OneDrive      │
                       │ • To Do         │
                       │ • SharePoint    │
                       └─────────────────┘
```

### Data Flow Architecture
```
User Input ──▶ Intent Analysis ──▶ Function Selection ──▶ Graph APIs ──▶ UI Response
     │              │                    │                    │              │
     ▼              ▼                    ▼                    ▼              ▼
┌───────────┐  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  ┌─────────────┐
│ Natural   │  │ AI Analysis │    │ SK Plugins  │    │ MS Graph    │  │ React UI    │
│ Language  │  │ • Intent    │    │ • ToDo      │    │ • Secure    │  │ • Cards     │
│ Query     │  │ • Context   │    │ • Mail      │    │ • OAuth     │  │ • Analysis  │
│           │  │ • Params    │    │ • Calendar  │    │ • Real-time │  │ • Dynamic   │
└───────────┘  └─────────────┘    └─────────────┘    └─────────────┘  └─────────────┘
```

### MCP Integration Flow
```
SharePoint Query ──▶ MCP Client ──▶ MCP Server ──▶ SharePoint API ──▶ JSON Response
       │                  │              │               │               │
       ▼                  ▼              ▼               ▼               ▼
┌─────────────┐    ┌─────────────┐ ┌─────────────┐ ┌──────────────┐ ┌──────────┐
│ User asks   │    │ SSE Client  │ │ Tool Exec   │ │ SharePoint   │ │ Site     │
│ "SP sites"  │    │ Transport   │ │ Discovery   │ │ REST API +   │ │ Cards    │
│             │    │ Connection  │ │ & Execution │ │ SP OAuth     │ │ Rendered │
└─────────────┘    └─────────────┘ └─────────────┘ └──────────────┘ └──────────┘
```

**Note:** SharePoint integration uses both Microsoft Graph Sites.Read.All and dedicated SharePoint API permissions for comprehensive content access.

## ✨ Key Features

### 🤖 **Intelligent Conversational Interface**
- **Auto Function Calling**: AI automatically selects and calls appropriate Microsoft Graph APIs
- **Structured Output**: JSON-based responses with consistent card formatting
- **Analysis Mode**: AI-generated summaries and insights from your Microsoft 365 data
- **Context Awareness**: Multi-turn conversations with session memory
- **Smart UI**: Dynamic card rendering based on data type and user intent

### 🔗 **Microsoft 365 Integration**
- **📧 Email**: Read, search, compose emails with thread analysis
- **📅 Calendar**: View events, schedule meetings, check availability
- **📁 OneDrive**: Browse files, search content, manage documents
- **📝 Tasks**: Create and manage To Do items with priorities and due dates
- **🔍 SharePoint**: Advanced site discovery, content search, and taxonomy access via MCP protocol with dedicated SharePoint API permissions

### 🛡️ **Enterprise Security & Authentication**
- **Azure AD Authentication**: Secure MSAL-based token handling
- **Graph API Scopes**: Properly scoped permissions for each service
- **User Context**: All API calls made with user's access token
- **Token Management**: Automatic refresh and secure kernel data storage
- **Multi-tenant Ready**: Dynamic tenant discovery without hardcoding

### 🔌 **Modern Architecture**
- **Semantic Kernel**: Microsoft's orchestration framework for AI function calling
- **MCP Protocol**: Modular tool integration for SharePoint operations
- **Plugin System**: Extensible architecture for new Microsoft 365 services
- **TypeScript Frontend**: Type-safe React application with modern tooling
- **Structured Data Flow**: Consistent kernel data passing for UI rendering

## 🛠️ Technology Stack

### Frontend
- **React 18** with TypeScript
- **Vite** for fast development and building
- **Tailwind CSS** for styling
- **MSAL React** for Azure AD authentication
- **Modern Hooks** for state management

### Backend
- **ASP.NET Core 8.0** Web API
- **Microsoft Semantic Kernel** for AI orchestration
- **Microsoft Graph SDK** for Microsoft 365 integration
- **Model Context Protocol (MCP)** for SharePoint tools
- **OpenAI SDK** for GPT-4 integration

### AI & Integration
- **Azure OpenAI** with GPT-4 for natural language processing
- **Function Calling** for automatic API selection
- **Structured Output** with JSON schema validation
- **Microsoft Graph API** for Microsoft 365 services integration
- **SharePoint REST API** for advanced SharePoint operations via MCP

## 🚀 Quick Start

### Prerequisites

1. **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Node.js 18+** and npm - [Download](https://nodejs.org/)
3. **Azure AD App Registration** (see Azure Configuration below)
4. **Azure OpenAI Service** or **OpenAI API** with GPT-4 access

### Quick Setup Scripts

**Linux/macOS:**
```bash
# One-time setup
./setup-dev.sh

# Daily development
./start-all.sh       # Start all services
./status.sh          # Check service status  
./stop-all.sh        # Stop all services
./get-ports.sh       # Show port configuration
```

**Windows:**
```powershell
# One-time setup
.\setup-dev.ps1

# Daily development  
.\start-all.ps1      # Start all services
.\status.ps1         # Check service status
.\stop-all.ps1       # Stop all services
.\get-ports.ps1      # Show port configuration
```

### Service Endpoints

After running `./start-all.sh`:

- **🌐 React App**: https://localhost:31337
- **🔧 API Server**: https://localhost:31338
- **📊 API Docs**: https://localhost:31338/swagger
- **🔍 MCP Server**: https://localhost:31339

## ⚙️ Configuration

### Azure AD App Registration

1. **Create App Registration** in Azure Portal:
   ```
   Azure Active Directory → App registrations → New registration
   Name: "Semantic Kernel PoC"
   Redirect URI: https://localhost:31337 (Single-page application)
   ```

2. **Configure API Permissions**:
   
   **Microsoft Graph - Delegated permissions:**
   ```
   ✅ User.Read                 (Basic profile)
   ✅ Mail.Read                 (Read emails)
   ✅ Mail.Send                 (Send emails)
   ✅ Calendars.Read            (Read calendar)
   ✅ Calendars.ReadWrite       (Manage calendar)
   ✅ Files.Read.All            (Read OneDrive files)
   ✅ Sites.Read.All            (Read SharePoint sites)
   ✅ Tasks.ReadWrite           (Manage To Do tasks)
   ```

   **SharePoint - Delegated permissions:**
   ```
   ✅ Sites.Read.All            (Read SharePoint sites and content)
   ✅ Sites.Search.All          (Search SharePoint content)
   ✅ TermStore.Read.All        (Read SharePoint taxonomy)
   ```

3. **Grant Admin Consent** (if required by your organization)

**Note:** SharePoint permissions are used by the MCP server for advanced SharePoint search and content discovery beyond what's available through Microsoft Graph APIs.

### Application Configuration

**API Configuration** (`SemanticKernelPoc.Api/appsettings.Development.json`):
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE"
  },
  "SemanticKernel": {
    "UseAzureOpenAI": false,
    "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
    "DeploymentOrModelId": "gpt-4o-mini",
    "Endpoint": ""
  },
  "McpServer": {
    "Url": "https://localhost:31339"
  }
}
```

**Frontend Configuration** (`SemanticKernelPoc.Web/src/config/config.local.json`):
```json
{
  "azure": {
    "tenantId": "YOUR_TENANT_ID_HERE",
    "clientId": "YOUR_CLIENT_ID_HERE"
  },
  "app": {
    "redirectUri": "https://localhost:31337"
  }
}
```

## 📁 Project Structure

```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/              # 🌐 ASP.NET Core Web API
│   ├── Controllers/
│   │   └── ChatController.cs           #   • Main chat endpoint with Semantic Kernel
│   ├── Plugins/                        #   • Microsoft Graph plugins
│   │   ├── Calendar/CalendarPlugin.cs  #   • Calendar events and scheduling
│   │   ├── Mail/MailPlugin.cs          #   • Email management and search
│   │   ├── OneDrive/OneDrivePlugin.cs  #   • File operations and search
│   │   └── ToDo/ToDoPlugin.cs          #   • Task management with To Do
│   ├── Services/
│   │   ├── Graph/                      #   • Microsoft Graph integration
│   │   ├── Memory/                     #   • Conversation memory
│   │   └── Shared/                     #   • Card building and analysis services
│   └── Models/                         #   • Structured response models
├── SemanticKernelPoc.McpServer/        # 🔧 Model Context Protocol Service
│   ├── Tools/
│   │   └── SharePointSearchTool.cs     #   • SharePoint search via MCP
│   ├── Services/
│   │   └── SharePointSearchService.cs  #   • SharePoint Graph API integration
│   └── Models/                         #   • SharePoint data models
├── SemanticKernelPoc.Web/              # ⚛️  React Frontend
│   ├── src/
│   │   ├── components/                 #   • Chat interface and card components
│   │   ├── config/                     #   • Azure AD configuration
│   │   ├── hooks/                      #   • Authentication and API hooks
│   │   └── services/                   #   • API communication services
│   ├── package.json                    #   • Dependencies and build scripts
│   └── vite.config.ts                  #   • Vite configuration
├── certs/                              # 🔐 HTTPS certificates
├── logs/                               # 📋 Application logs
├── setup-dev.sh/ps1                   # 🛠️  Development environment setup
├── start-all.sh/ps1                   # 🚀 Start all services
├── stop-all.sh/ps1                    # 🛑 Stop all services
├── status.sh/ps1                      # 📊 Check service status
└── get-ports.sh/ps1                   # 🔌 Port configuration info
```

## 🎯 Usage Examples

### Task Management
```
👤 User: "Show me my tasks for this week"
🤖 AI: Automatically calls ToDoPlugin.GetRecentNotes()
📋 Result: Task cards with priorities, due dates, and completion status
```

### Email Analysis  
```
👤 User: "Summarize my emails from John about the project"
🤖 AI: Calls MailPlugin.SearchEmails() + Analysis mode
📧 Result: AI-generated summary of email conversations
```

### Calendar Planning
```
👤 User: "What's my schedule for tomorrow?"
🤖 AI: Calls CalendarPlugin.GetRecentEvents()
📅 Result: Calendar cards with meeting details and availability
```

### SharePoint Discovery
```
👤 User: "Find SharePoint sites for our Q4 planning"
🤖 AI: Calls SharePoint MCP tools via protocol
🔍 Result: Relevant SharePoint sites with metadata
```

### File Search
```
👤 User: "Find my PowerPoint presentations about sales"
🤖 AI: Calls OneDrivePlugin.SearchFiles()
📁 Result: File cards with download links and metadata
```

## 🔄 Application Flow

### Authentication Flow
```
1. User opens React app → 2. MSAL redirects to Azure AD → 3. User signs in
                              ↓
4. Access token received ← 3. Token validation ← 2. Authorization code returned
                              ↓
5. Token stored in React state and sent with each API request
```

### Chat Interaction Flow
```
1. User Input
   ┌─────────────────┐
   │ "Show my tasks" │
   └─────────────────┘
           │
           ▼
2. React Frontend
   ┌─────────────────┐
   │ POST /api/chat  │
   │ + Bearer token  │
   └─────────────────┘
           │
           ▼
3. ChatController
   ┌─────────────────┐
   │ • Parse request │
   │ • Create kernel │
   │ • Set user data │
   └─────────────────┘
           │
           ▼
4. Semantic Kernel
   ┌─────────────────┐
   │ • Analyze input │
   │ • Select plugin │
   │ • Call function │
   └─────────────────┘
           │
           ▼
5. Plugin Execution
   ┌─────────────────┐
   │ • Graph API     │
   │ • Process data  │
   │ • Format output │
   └─────────────────┘
           │
           ▼
6. Structured Response
   ┌─────────────────┐
   │ • Cards data    │
   │ • Text response │
   │ • Metadata      │
   └─────────────────┘
           │
           ▼
7. React UI Rendering
   ┌─────────────────┐
   │ • Render cards  │
   │ • Display text  │
   │ • Update state  │
   └─────────────────┘
```

## 🔧 Development

### Adding New Plugins

1. **Create Plugin Class**:
```csharp
public class YourPlugin : BaseGraphPlugin
{
    [KernelFunction]
    [Description("Your function description")]
    public async Task<string> YourFunction(
        Kernel kernel,
        [Description("Parameter description")] string parameter)
    {
        // Implementation using Microsoft Graph
    }
}
```

2. **Register in ChatController**:
```csharp
var yourPlugin = new YourPlugin(graphService, graphClientFactory, logger);
kernel.Plugins.AddFromObject(yourPlugin, "YourPlugin");
```

### Debugging and Monitoring

```bash
# Real-time log monitoring
tail -f logs/api-server.log      # API service logs
tail -f logs/mcp-server.log      # MCP service logs  

# Debug specific components
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project SemanticKernelPoc.Api --verbosity detailed
```

### Testing MCP Integration

```bash
# Test MCP server directly
curl https://localhost:31339/sse

# Check tool discovery
curl https://localhost:31339/tools

# Verify SharePoint authentication
# Check logs for token validation
```

## 🚨 Troubleshooting

### Authentication Issues
- Verify Azure AD app registration configuration
- Check redirect URI matches exactly: `https://localhost:31337`
- Ensure proper API permissions are granted and consented:
  - **Microsoft Graph**: User.Read, Mail.Read, Mail.Send, Calendars.Read, Calendars.ReadWrite, Files.Read.All, Sites.Read.All, Tasks.ReadWrite
  - **SharePoint**: Sites.Read.All, Sites.Search.All, TermStore.Read.All
- Verify tenant ID and client ID in both config files
- Check that admin consent has been granted for SharePoint permissions

### Service Startup Issues
```bash
# Check port availability
./get-ports.sh

# Kill processes using required ports
lsof -i :31337 | awk 'NR>1 {print $2}' | xargs kill -9
lsof -i :31338 | awk 'NR>1 {print $2}' | xargs kill -9
lsof -i :31339 | awk 'NR>1 {print $2}' | xargs kill -9
```

### SSL Certificate Issues
```bash
# Regenerate certificates
rm -rf certs/
./start-all.sh  # Will regenerate automatically

# Trust certificates (macOS)
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/localhost.crt
```

### MCP Connection Issues
- Verify MCP server is running on port 31339
- Check SharePoint authentication configuration and permissions:
  - Ensure Sites.Read.All, Sites.Search.All, and TermStore.Read.All permissions are granted
  - Verify admin consent for SharePoint-specific permissions
  - Check that the service account has access to SharePoint sites
- Review MCP server logs for specific SharePoint API errors
- Ensure user has SharePoint access permissions for target sites
- Test SharePoint connectivity: `curl https://localhost:31339/tools`

## 📈 Performance Considerations

- **Token Caching**: Implement token cache for production deployments
- **Rate Limiting**: Microsoft Graph APIs have rate limits
- **Connection Pooling**: Configure HTTP client connection pooling
- **Logging**: Adjust log levels for production environments
- **Memory Management**: Monitor Semantic Kernel memory usage

## 🔐 Security Best Practices

- **Secrets Management**: Use Azure Key Vault for production
- **Token Validation**: Implement proper JWT token validation
- **CORS Configuration**: Restrict CORS to known origins
- **HTTPS Only**: Enforce HTTPS in production
- **Audit Logging**: Log all Microsoft Graph API access

## 🤝 Contributing

This proof-of-concept demonstrates modern AI integration patterns. Areas for extension:

- **New Microsoft 365 Services**: Teams, Planner, Yammer integration
- **Enhanced AI Models**: Experiment with different GPT models and parameters
- **Advanced UI**: Improve card designs and interaction patterns
- **Performance Optimization**: Implement caching and optimization strategies
- **Testing**: Add comprehensive unit and integration tests

## 📄 License

This project is provided as-is for educational and proof-of-concept purposes.

---

🎉 **Ready to start!** 

1. Run `./setup-dev.sh` (or `.ps1` on Windows)
2. Run `./start-all.sh` to launch all services
3. Visit https://localhost:31337 to start chatting with your AI assistant!

The AI will automatically call the appropriate Microsoft 365 APIs based on your natural language requests and display the results as interactive cards.