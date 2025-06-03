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

**Note:** SharePoint integration uses SharePoint Search REST API (`/_api/search/query`) with Sites.Search.All permissions for comprehensive site discovery and search capabilities. Microsoft Graph is only used for tenant discovery and token exchange.

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
- **🔍 SharePoint**: Site discovery and content search via MCP protocol with dedicated SharePoint API permissions

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

#### 1. **Create App Registration** in Azure Portal:
```
Azure Active Directory → App registrations → New registration
Name: "Semantic Kernel PoC"
Supported account types: Accounts in this organizational directory only (Single tenant)
```

#### 2. **Configure Redirect URIs** (Authentication Tab):

**Platform: Single-page application (SPA)**
```
✅ https://localhost:31337                     (React client authentication)
```

**Platform: Web**
```
✅ https://localhost:31338/signin-oidc         (API OIDC callback)
```

**Logout URLs (optional)**
```
✅ https://localhost:31337                     (Post-logout redirect)
```

**Advanced settings:**
- ✅ **Allow public client flows**: Yes (enables device code flow)
- ✅ **Enable the following mobile and desktop flows**: Yes

#### 3. **API Permissions** (API permissions Tab):

**Microsoft Graph - Delegated permissions:**
```
✅ openid                    (Sign in and read user profile)
✅ profile                   (View users' basic profile)  
✅ email                     (View users' email address)
✅ User.Read                 (Read user profile)
✅ Mail.Read                 (Read user mail)
✅ Mail.Send                 (Send mail as user)
✅ Calendars.Read            (Read user calendars)
✅ Calendars.ReadWrite       (Have full access to user calendars)
✅ Files.Read.All            (Read files in all site collections)
✅ Sites.Read.All            (Read items in all site collections)
✅ Tasks.ReadWrite           (Create, read, update and delete user tasks and projects)
```

**SharePoint - Delegated permissions:**
```
✅ Sites.Search.All          (Search SharePoint content)
```

**Office 365 Exchange Online - Delegated permissions:**
```
✅ EWS.AccessAsUser.All      (Access Exchange Web Services as user)
```

#### 4. **Grant Admin Consent**
- Click **"Grant admin consent for [Your Organization]"**
- Confirm: "Yes" to grant consent for all users in the organization

#### 5. **Expose an API** (Expose an API Tab):

**Application ID URI:**
```
api://[YOUR_CLIENT_ID]
```

**Scopes defined by this API:**
```
Scope name: access_as_user
Admin consent display name: Access API as user
Admin consent description: Allow the application to access the API on behalf of the signed-in user
Value: access_as_user
State: Enabled
Who can consent: Admins and users
```

**Authorized client applications:**
```
Client ID: [YOUR_CLIENT_ID]  (Self-authorization for SPA)
Authorized scopes: api://[YOUR_CLIENT_ID]/access_as_user
```

#### 6. **Certificates & Secrets** (Certificates & secrets Tab):

**Client secrets (for MCP server and API):**
```
Description: "SemanticKernelPoc API Secret"
Expires: 12 months (or according to your security policy)
```
⚠️ **Important**: Copy the secret value immediately - it won't be shown again!

#### 7. **Token Configuration** (Token configuration Tab):

**Optional claims - ID token:**
```
✅ email          (Email address)
✅ family_name    (Surname)  
✅ given_name     (Given name)
✅ upn           (User Principal Name)
```

**Optional claims - Access token:**
```
✅ email          (Email address)
✅ family_name    (Surname)
✅ given_name     (Given name)
✅ upn           (User Principal Name)
```

#### 8. **Manifest Configuration** (Manifest Tab):

**Critical manifest settings:**
```json
{
  "accessTokenAcceptedVersion": 2,
  "oauth2RequirePostResponse": false,
  "oauth2AllowImplicitFlow": true,
  "oauth2AllowIdTokenImplicitFlow": true,
  "signInAudience": "AzureADMyOrg"
}
```

**Key changes to make:**
- Set `"accessTokenAcceptedVersion": 2` (OAuth 2.0 tokens)
- Set `"oauth2AllowImplicitFlow": true` (for SPA authentication)
- Set `"oauth2AllowIdTokenImplicitFlow": true` (for ID tokens)

#### 9. **API Permissions Summary**

**Why each permission is needed:**

| Permission | Service | Reason |
|------------|---------|---------|
| `User.Read` | Graph | Basic user profile for authentication |
| `Mail.Read` | Graph | Email plugin functionality |
| `Mail.Send` | Graph | Email composition features |
| `Calendars.Read` | Graph | Calendar viewing |
| `Calendars.ReadWrite` | Graph | Calendar management |
| `Files.Read.All` | Graph | OneDrive file access |
| `Sites.Read.All` | Graph | SharePoint sites via Graph API |
| `Tasks.ReadWrite` | Graph | Microsoft To Do integration |
| `Sites.Search.All` | SharePoint | SharePoint search via REST API |
| `EWS.AccessAsUser.All` | Exchange | Advanced email operations |

#### 10. **Authentication Flows Used**

**React SPA (Frontend):**
- **Flow**: Authorization Code Flow with PKCE
- **Redirect**: `https://localhost:31337`
- **Tokens**: ID token + Access token

**API Server (Backend):**
- **Flow**: On-Behalf-Of (OBO) flow
- **Redirect**: `https://localhost:31338/signin-oidc`
- **Tokens**: Exchange user token for Graph/SharePoint tokens

**MCP Server:**
- **Flow**: On-Behalf-Of (OBO) flow
- **Purpose**: SharePoint Search API access
- **Tokens**: Exchange user token for SharePoint tokens

#### 11. **Configuration Validation**

**Test your configuration:**
```bash
# 1. Check redirect URIs are exactly configured
curl https://login.microsoftonline.com/[TENANT_ID]/.well-known/openid_configuration

# 2. Test MSAL configuration
./get-ports.sh  # Shows required redirect URIs

# 3. Verify API exposure
# Go to Azure Portal → Your App → Expose an API
# Ensure api://[CLIENT_ID]/access_as_user scope exists
```


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

**Azure AD App Registration:**
- ✅ Verify app registration is configured as **Single tenant** (`AzureADMyOrg`)
- ✅ Check redirect URIs match exactly: 
  - `https://localhost:31337` (Single-page application platform)
  - `https://localhost:31338/signin-oidc` (Web platform)
- ✅ Verify **Application ID URI**: `api://[YOUR_CLIENT_ID]`
- ✅ Ensure **API scope** exists: `api://[YOUR_CLIENT_ID]/access_as_user`

**API Permissions & Consent:**
- ✅ All required permissions are added (see detailed list above)
- ✅ **Admin consent granted** for the organization
- ✅ **SharePoint permissions**: `Sites.Search.All` has admin consent
- ✅ **Microsoft Graph permissions**: All delegated permissions consented

**Manifest Configuration:**
- ✅ `"accessTokenAcceptedVersion": 2` (OAuth 2.0 tokens)
- ✅ `"oauth2AllowImplicitFlow": true` (SPA authentication)
- ✅ `"oauth2AllowIdTokenImplicitFlow": true` (ID token flow)
- ✅ `"signInAudience": "AzureADMyOrg"` (single tenant)

**Configuration Files:**
- ✅ Verify tenant ID and client ID in both config files match Azure AD app
- ✅ Check `api://[CLIENT_ID]/access_as_user` scope is correct in frontend config
- ✅ Ensure API audience is set to `api://[CLIENT_ID]` in backend config

**Common Token Issues:**
```bash
# IDX14100: JWT is not well formed - token missing dots
# → Check token is being passed correctly from frontend to API

# MSAL errors - invalid scope
# → Verify api://[CLIENT_ID]/access_as_user scope exists and is authorized

# Audience validation failed  
# → Check API audience configuration in appsettings.json
```

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
  - Ensure Sites.Search.All permissions are granted
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