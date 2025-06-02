# 🤖 Semantic Kernel PoC - Microsoft 365 AI Assistant

A comprehensive proof-of-concept application demonstrating the integration of **Microsoft Semantic Kernel** with **Microsoft Graph API** and **Model Context Protocol (MCP)** to create an intelligent AI assistant for Microsoft 365 productivity tasks.

## 📋 Overview

This application provides a conversational AI interface that can interact with your Microsoft 365 data through natural language. The AI assistant can help you manage calendars, emails, SharePoint sites, OneDrive content, and tasks using Microsoft To Do, featuring structured output responses and intelligent intent classification.

## 🏗️ Architecture

### System Architecture
```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│    React Client     │     │   ASP.NET Core      │     │     MCP Server      │
│                     │     │       API           │     │                     │
│ • Authentication    │────▶│ • Semantic Kernel   │────▶│ • SharePoint API    │
│ • Chat Interface    │     │ • Intent Detection  │     │ • Tenant Discovery  │
│ • Card Rendering    │     │ • Plugin System     │     │ • Token Management  │
│                     │     │ • Structured Output │     │                     │
└─────────────────────┘     └─────────────────────┘     └─────────────────────┘
         │                           │                           │
         │                           ▼                           │
         │                  ┌─────────────────────┐              │
         │                  │   Microsoft Graph   │              │
         └─────────────────▶│                     │◀─────────────┘
                            │ • Authentication    │
                            │ • Mail              │
                            │ • Calendar          │
                            │ • OneDrive          │
                            │ • To Do             │
                            │ • SharePoint        │
                            └─────────────────────┘
```

### Information Flow
```
User Input ──▶ Intent Classification ──▶ Function Selection ──▶ Microsoft 365 APIs ──▶ Structured Response
     │                │                        │                        │                     │
     │                ▼                        ▼                        ▼                     ▼
     │        ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
     │        │ AI classifies:  │    │ Semantic Kernel │    │ Graph API calls │    │ Cards/Analysis  │
     │        │ • Intent type   │    │ executes:       │    │ via Graph SDK   │    │ rendered in UI  │
     │        │ • Data type     │    │ • Mail plugin   │    │ • Secured with  │    │ • Task cards    │
     └───────▶│ • Confidence    │───▶│ • Calendar      │───▶│   user tokens   │───▶│ • Email cards   │
              │ • Parameters    │    │ • SharePoint    │    │ • Proper scopes │    │ • Calendar      │
              │ • UI format     │    │ • OneDrive      │    │ • Tenant aware  │    │ • Analysis text │
              └─────────────────┘    │ • To Do tasks   │    └─────────────────┘    └─────────────────┘
                                     └─────────────────┘
```

## ✨ Key Features

### 🤖 **Intelligent Conversational Interface**
- **Structured Output**: AI responses use JSON schema for consistent formatting
- **Intent Classification**: Automatic detection of user intent (list, search, create, analyze)
- **Context Awareness**: Multi-turn conversations with session memory
- **Smart UI**: Dynamic card rendering based on response type

### 🔗 **Microsoft 365 Integration**
- **📧 Email**: Read, search, compose, and analyze email threads
- **📅 Calendar**: View events, schedule meetings, check availability
- **📁 OneDrive**: Browse files, search content, manage documents
- **📝 Tasks**: Create and manage To Do items with priorities and due dates
- **🔍 SharePoint**: Find sites and content across tenant with MCP service

### 🛡️ **Enterprise Security**
- **Azure AD Authentication**: Secure token handling with MSAL
- **On-Behalf-Of Flow**: Service-to-service calls with user context
- **Dynamic Tenant Discovery**: Multi-tenant support without hardcoding
- **Token Management**: Automatic refresh and secure storage

### 🔌 **Extensible Architecture**
- **Plugin System**: Modular functionality with Semantic Kernel plugins
- **MCP Integration**: Model Context Protocol for SharePoint operations
- **Structured Responses**: Type-safe AI responses with JSON schema
- **Function Calling**: Automatic API selection based on user intent

## 🛠️ Technology Stack

- **Frontend**: React 18, TypeScript, Vite, Tailwind CSS
- **Backend**: ASP.NET Core 8.0, Semantic Kernel, Microsoft Graph SDK
- **AI**: Azure OpenAI with structured output and function calling
- **Authentication**: Microsoft Identity Platform (Azure AD)
- **Architecture**: Model Context Protocol (MCP) for modular services

## 🚀 Complete Setup Guide

### Prerequisites

1. **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Node.js 18+** and npm - [Download](https://nodejs.org/)
3. **Azure AD App Registration** (see Azure Configuration below)
4. **Azure OpenAI Service** with GPT-4 deployment

### Azure Configuration

#### 1. Azure AD App Registration

1. **Create App Registration**:
   ```
   Azure Portal → Azure Active Directory → App registrations → New registration
   Name: "Semantic Kernel PoC"
   Redirect URI: https://localhost:31337 (Single-page application)
   ```

2. **Configure API Permissions**:
   ```
   API permissions → Add permission → Microsoft Graph → Delegated permissions:
   
   ✅ User.Read                 (Basic profile)
   ✅ Mail.Read                 (Read emails)
   ✅ Mail.Send                 (Send emails)
   ✅ Calendars.Read            (Read calendar)
   ✅ Calendars.ReadWrite       (Manage calendar)
   ✅ Files.Read.All            (Read OneDrive files)
   ✅ Sites.Read.All            (Read SharePoint sites)
   ✅ Tasks.ReadWrite           (Manage To Do tasks)
   ```

3. **Grant Admin Consent** (if required by your organization)

4. **Note these values**:
   - **Tenant ID**: `Directory (tenant) ID` from Overview page
   - **Client ID**: `Application (client) ID` from Overview page

#### 2. Azure OpenAI Service

1. **Create Azure OpenAI Resource**:
   ```
   Azure Portal → Create resource → Azure OpenAI → Create
   ```

2. **Deploy GPT-4 Model**:
   ```
   Azure OpenAI Studio → Deployments → Create new deployment
   Model: gpt-4 or gpt-4-turbo
   Deployment name: gpt-4 (remember this name)
   ```

3. **Note these values**:
   - **Endpoint**: From resource overview (e.g., `https://your-resource.openai.azure.com/`)
   - **API Key**: From Keys and Endpoint section

### Local Development Setup

#### 1. Clone and Install Dependencies

```bash
# Clone repository
git clone <your-repository-url>
cd SemanticKernelPoc

# Install .NET dependencies
dotnet restore

# Install Node.js dependencies
cd SemanticKernelPoc.Web
npm install
cd ..
```

#### 2. Configure API Settings

```bash
# Copy template to create your config
cp SemanticKernelPoc.Api/appsettings.Development.template.json \
   SemanticKernelPoc.Api/appsettings.Development.json
```

Edit `SemanticKernelPoc.Api/appsettings.Development.json`:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE"
  },
  "SemanticKernel": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
      "DeploymentName": "gpt-4"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

#### 3. Configure React Client

```bash
# Copy template to create your config
cp SemanticKernelPoc.Web/src/config/config.example.json \
   SemanticKernelPoc.Web/src/config/config.local.json
```

Edit `SemanticKernelPoc.Web/src/config/config.local.json`:
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

#### 4. Setup HTTPS Certificates

The startup script will automatically generate self-signed certificates. To trust them:

**macOS**:
```bash
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/localhost.crt
```

**Windows** (PowerShell as Administrator):
```powershell
Import-Certificate -FilePath "certs\localhost.crt" -CertStoreLocation Cert:\LocalMachine\Root
```

**Linux**:
```bash
sudo cp certs/localhost.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates
```

#### 5. Start the Application

```bash
# Start all services (API, MCP Server, React Client)
./start-all.sh
```

**Access Points**:
- **React App**: https://localhost:31337
- **API Documentation**: https://localhost:31338/swagger
- **MCP Server**: http://localhost:3001 (internal)

#### 6. Stop the Application

```bash
./stop-all.sh
```

### Configuration Security

#### File Structure
```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/
│   ├── appsettings.json                        # ✅ Base config (committed)
│   ├── appsettings.Development.template.json  # ✅ Template (committed)
│   └── appsettings.Development.json           # 🔒 Your secrets (git-ignored)
├── SemanticKernelPoc.Web/src/config/
│   ├── config.example.json                    # ✅ Template (committed)
│   └── config.local.json                      # 🔒 Your secrets (git-ignored)
```

#### Security Features
- ✅ **Git-ignored**: Secret files never committed to version control
- ✅ **Template-based**: Clear structure for required configuration
- ✅ **Fallback safe**: App works with defaults if local config missing
- ✅ **No environment variables**: Simple file-based approach

### Troubleshooting Common Issues

#### Authentication Issues
```bash
# Check your Azure AD configuration
# Verify tenant ID and client ID in both config files
# Ensure redirect URI matches exactly: https://localhost:31337
```

#### Certificate Issues
```bash
# Regenerate certificates
rm -rf certs/
./start-all.sh  # Will regenerate automatically

# Re-trust certificates (see HTTPS setup above)
```

#### Port Conflicts
```bash
# Check if ports are in use
lsof -i :31337  # React (frontend)
lsof -i :31338  # API (backend)
lsof -i :3001   # MCP Server

# Kill conflicting processes
kill -9 <PID>
```

#### Permission Issues
```bash
# Verify Azure AD permissions are granted
# Check admin consent status in Azure Portal
# Ensure user has access to SharePoint/OneDrive
```

## 📁 Project Structure

```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/              # 🌐 ASP.NET Core Web API
│   ├── Controllers/                    #   • Chat endpoint with structured output
│   ├── Plugins/                        #   • Semantic Kernel plugins for M365
│   │   ├── Calendar/                   #   • Calendar operations
│   │   ├── Mail/                       #   • Email management
│   │   ├── OneDrive/                   #   • File operations
│   │   ├── SharePoint/                 #   • SharePoint MCP integration
│   │   └── ToDo/                       #   • Task management
│   ├── Services/                       #   • Intent detection & analysis
│   └── Models/                         #   • Structured response models
├── SemanticKernelPoc.McpServer/        # 🔧 Model Context Protocol Service
│   ├── Tools/                          #   • SharePoint search tools
│   ├── Services/                       #   • Tenant discovery & auth
│   └── Models/                         #   • SharePoint data models
├── SemanticKernelPoc.Web/              # ⚛️  React Frontend
│   ├── src/
│   │   ├── components/                 #   • Chat interface & card components
│   │   ├── config/                     #   • Azure AD configuration
│   │   ├── hooks/                      #   • Authentication & API hooks
│   │   └── services/                   #   • API communication
│   └── package.json                    #   • Dependencies & scripts
├── certs/                              # 🔐 HTTPS certificates
├── logs/                               # 📋 Application logs
├── start-all.sh                        # 🚀 Start all services
├── stop-all.sh                         # 🛑 Stop all services
└── README.md                           # 📖 This documentation
```

## 🔄 Application Flow

### User Interaction Flow
```
1. User Authentication
   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
   │ User opens  │───▶│ Azure AD    │───▶│ React app   │
   │ React app   │    │ login flow  │    │ receives    │
   │ in browser  │    │ (MSAL)      │    │ auth tokens │
   └─────────────┘    └─────────────┘    └─────────────┘

2. Chat Interaction
   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
   │ User types  │───▶│ React sends │───▶│ API receives│
   │ message in  │    │ request to  │    │ message +   │
   │ chat box    │    │ /api/chat   │    │ user token  │
   └─────────────┘    └─────────────┘    └─────────────┘

3. AI Processing
   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
   │ Intent      │───▶│ Semantic    │───▶│ Plugin      │
   │ detection   │    │ Kernel      │    │ execution   │
   │ (AI-based)  │    │ orchestrates│    │ (Graph API) │
   └─────────────┘    └─────────────┘    └─────────────┘

4. Response Generation
   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
   │ Structured  │───▶│ Cards/text  │───▶│ UI renders  │
   │ response    │    │ formatted   │    │ for React   │
   │ (JSON)      │    │ for React   │    │ with cards  │
   └─────────────┘    └─────────────┘    └─────────────┘
```

### Technical Flow
```
Authentication (Azure AD) ──▶ Chat Interface (React) ──▶ API Gateway (ASP.NET)
                                       │                          │
                                       ▼                          ▼
User Input ────────────────────▶ Intent Classification ──▶ Semantic Kernel
                                       │                          │
                                       ▼                          ▼
Plugin Selection ◀──────────── Function Calling ◀────── AI Orchestration
     │                                                            │
     ▼                                                            ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐     ┌─────────────┐
│   Email     │  │  Calendar   │  │ SharePoint  │ ... │   To Do     │
│   Plugin    │  │   Plugin    │  │ MCP Server  │     │   Plugin    │
└─────────────┘  └─────────────┘  └─────────────┘     └─────────────┘
     │                  │                  │                   │
     ▼                  ▼                  ▼                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Microsoft Graph API                             │
│  • Authentication  • Mail  • Calendar  • OneDrive  • SharePoint    │
└─────────────────────────────────────────────────────────────────────┘
```

## 🎯 Usage Examples

### Task Management
```
User: "Show me my tasks for this week"
→ Intent: list, task, confidence: 0.95
→ Plugin: ToDoPlugin.GetRecentNotes
→ Result: Task cards with priorities and due dates
```

### Email Analysis
```
User: "Summarize my emails from John about the project"
→ Intent: analyze, email, confidence: 0.90
→ Plugin: MailPlugin.SearchEmails + Analysis
→ Result: AI-generated summary of email threads
```

### Calendar Coordination
```
User: "When is my next free 1-hour slot tomorrow?"
→ Intent: search, calendar, confidence: 0.85
→ Plugin: CalendarPlugin.GetUpcomingEvents
→ Result: Available time slots with calendar context
```

### SharePoint Discovery
```
User: "Find SharePoint sites related to our Q4 planning"
→ Intent: search, sharepoint, confidence: 0.92
→ Plugin: SharePointMcpPlugin via MCP Server
→ Result: Relevant SharePoint sites with descriptions
```

## 🔧 Advanced Configuration

### Logging and Monitoring
```bash
# Real-time log monitoring
tail -f logs/api-server.log      # API service logs
tail -f logs/mcp-server.log      # MCP service logs  
tail -f logs/client.log          # React client logs

# Log locations
logs/
├── api-server.log               # ASP.NET Core API
├── mcp-server.log              # MCP SharePoint service
└── client.log                  # Vite development server
```

### Development Customization
```json
// appsettings.Development.json - Adjust log levels
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "SemanticKernelPoc": "Debug"    // Your app logs
    }
  }
}
```

### Plugin Development
```csharp
// Add new plugins in SemanticKernelPoc.Api/Plugins/
[KernelFunction]
[Description("Your plugin description")]
public async Task<string> YourFunction(
    [Description("Parameter description")] string parameter)
{
    // Implementation
}
```

## 🤝 Contributing

This proof-of-concept demonstrates modern AI integration with Microsoft 365. Key areas for extension:

- **New Plugins**: Add support for Teams, Planner, or other M365 services
- **Enhanced UI**: Improve card designs and interaction patterns
- **AI Models**: Experiment with different OpenAI models and parameters
- **Security**: Implement additional security layers and monitoring

## 📄 License

This project is provided as-is for educational and proof-of-concept purposes.

---

🎉 **You're all set!** Visit https://localhost:31337 to start chatting with your AI assistant. 