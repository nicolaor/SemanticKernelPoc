# 🤖 Semantic Kernel PoC - Microsoft 365 AI Assistant

A comprehensive proof-of-concept application demonstrating the integration of **Microsoft Semantic Kernel** with **Microsoft Graph API** to create an intelligent AI assistant for Microsoft 365 productivity tasks with advanced workflow automation and smart function selection.

## 📋 Overview

This application provides a conversational AI interface that can interact with your Microsoft 365 data through natural language. The AI assistant can help you manage your calendar, emails, SharePoint files, OneDrive content, create notes, access meeting transcripts, and execute complex cross-plugin workflows automatically.

### 🌟 Key Features

- **🗓️ Calendar Management**: View, create, update, and delete calendar events with rich card displays
- **📧 Email Operations**: Read, send, search emails and manage drafts with full metadata
- **📁 SharePoint Integration**: Browse sites, search files, and access documents across your organization
- **💾 OneDrive Access**: View drive information and manage files with comprehensive metadata
- **📝 Task & Note Management**: Create and manage tasks/notes using Microsoft To Do with card-based UI
- **🎙️ Meeting Transcript Analysis**: Access Teams meeting transcripts, generate summaries, extract decisions, and create action items
- **🔄 Cross-Plugin Workflows**: Automated business processes that chain multiple plugins together
- **🧠 Smart Function Selection**: Intelligent function selection based on context and conversation history
- **💬 Conversation Memory**: Maintains context across chat sessions with intelligent topic tracking
- **🎨 Modern UI**: Clean, responsive React TypeScript frontend with beautiful card-based displays
- **🔒 Secure Authentication**: Azure AD integration with comprehensive permission scoping

### 🚀 Advanced Capabilities

#### **Cross-Plugin Workflows**
Automated business processes that intelligently combine multiple Microsoft 365 services:

1. **Meeting to Tasks**: Extract action items from meeting transcripts and create To Do tasks
2. **Email to Calendar**: Create calendar events from email content automatically
3. **Project Planning**: Create project notes and schedule planning meetings
4. **Meeting Follow-up**: Generate meeting summaries and send follow-up emails
5. **Weekly Review**: Compile comprehensive weekly activity summaries

#### **Smart Function Selection**
- **Context-Aware**: Selects relevant functions based on conversation context
- **Performance Optimized**: Limits to top 8 functions to manage token usage
- **Keyword Matching**: Advanced keyword mapping for precise function selection
- **Workflow Integration**: Boosts function relevance based on active workflows

#### **Rich Card-Based UI**
- **Calendar Cards**: Beautiful event displays with attendee information, locations, and quick actions
- **Task Cards**: Comprehensive task management with priority, due dates, and status tracking
- **Meeting Transcript Cards**: Rich meeting content with summaries and action items
- **Email Cards**: Full email metadata with attachments and threading information

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   React Web     │    │   .NET 8 API     │    │  Microsoft      │
│   Frontend      │◄──►│   (Semantic      │◄──►│  Graph API      │
│   (TypeScript)  │    │    Kernel)       │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌──────────────────┐
                       │   Azure AD       │
                       │   Authentication │
                       └──────────────────┘
```

### 🔧 Technology Stack

**Backend:**
- .NET 8 Web API
- Microsoft Semantic Kernel 1.6.3
- Microsoft Graph SDK 5.56.0
- Microsoft Identity Web 2.17.2
- Azure AD Authentication

**Frontend:**
- React 18 with TypeScript
- Vite build system
- Tailwind CSS
- Microsoft Authentication Library (MSAL)

**AI Integration:**
- Azure OpenAI or OpenAI GPT models
- Semantic Kernel for function calling
- Microsoft Graph for M365 data access
- Smart Function Selection system
- Cross-plugin workflow orchestration
- Future-ready for Semantic Kernel Process Framework migration

## 🚀 Getting Started

### Prerequisites

- **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** - [Download here](https://nodejs.org/)
- **Azure Subscription** - [Get free account](https://azure.microsoft.com/free/)
- **Microsoft 365 Developer Tenant** (optional but recommended) - [Get free tenant](https://developer.microsoft.com/microsoft-365/dev-program)

### 🔐 Azure Setup

#### 1. Create Azure AD App Registration

1. Go to [Azure Portal](https://portal.azure.com) → **Azure Active Directory** → **App registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `SemanticKernelPoc`
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: 
     - Type: `Single-page application (SPA)`
     - URI: `http://localhost:5173` (for development)
4. Click **Register**
5. **Copy the Application (client) ID** - you'll need this for configuration

#### 2. Expose an API (Required for API Access)

1. Go to **Expose an API**
2. Click **Set** next to Application ID URI
3. Accept the default URI (e.g., `api://12345678-1234-1234-1234-123456789012`) or customize it
4. Click **Add a scope**
5. Configure the scope:
   - **Scope name**: `access_as_user`
   - **Who can consent**: Admins and users
   - **Admin consent display name**: `Access SemanticKernelPoc as a user`
   - **Admin consent description**: `Allow the application to access SemanticKernelPoc on behalf of the signed-in user`
   - **User consent display name**: `Access SemanticKernelPoc`
   - **User consent description**: `Allow the application to access SemanticKernelPoc on your behalf`
   - **State**: Enabled
6. Click **Add scope**

#### 3. Configure API Permissions

Add the following **Microsoft Graph** permissions:

**📅 Calendar Permissions:**
- `Calendars.Read` - Read user calendars
- `Calendars.ReadWrite` - Full calendar access

**📧 Email Permissions:**
- `Mail.Read` - Read user mail
- `Mail.ReadWrite` - Read and write mail
- `Mail.Send` - Send mail as user

**📁 File & SharePoint Permissions:**
- `Sites.Read.All` - Read SharePoint sites
- `Files.Read.All` - Read OneDrive and SharePoint files

**📝 Task & Note Permissions:**
- `Tasks.Read` - Read user tasks
- `Tasks.ReadWrite` - Read and write tasks (for notes/todos)

**🎙️ Meeting & Teams Permissions:**
- `OnlineMeetings.Read` - Read online meeting details
- `OnlineMeetingTranscript.Read.All` - Read meeting transcripts

**👤 User & Profile Permissions:**
- `User.Read` - Read user profile
- `User.ReadBasic.All` - Read basic user profiles

**🔍 Directory Permissions:**
- `Directory.Read.All` - Read directory data (for user lookups)

**Steps:**
1. Go to **API permissions** → **Add a permission**
2. Select **Microsoft Graph** → **Delegated permissions**
3. Add each permission listed above
4. **Important**: Click **Grant admin consent for [Your Organization]** (requires admin privileges)
5. Verify all permissions show "Granted for [Your Organization]" with green checkmarks

**⚠️ Note on CallRecords.Read.All:**
This permission is **NOT** available as a delegated permission and requires application permissions with special approval from Microsoft. The application works without this permission, but some advanced meeting analytics features may be limited.

#### 4. Configure Authentication

1. Go to **Authentication**
2. Under **Single-page application**, add redirect URIs:
   - `http://localhost:5173` (development)
   - Your production URL when deploying (e.g., `https://your-app.azurewebsites.net`)
3. Under **Advanced settings**:
   - ✅ Enable **Access tokens** (used by implicit flow)
   - ✅ Enable **ID tokens** (used by implicit flow)
4. Under **Supported account types**: Ensure "Accounts in this organizational directory only" is selected

#### 5. Configure Token Claims (Essential for User Information)

1. Go to **Token configuration**
2. Click **Add optional claim**
3. Select **ID** token type
4. Add the following claims:
   - ✅ `email` - User's email address
   - ✅ `family_name` - User's last name (maps to `surname` claim)
   - ✅ `given_name` - User's first name
   - ✅ `name` - User's display name (full name)
   - ✅ `preferred_username` - User's preferred username (usually email)
   - ✅ `upn` - User Principal Name
5. Click **Add** for each claim
6. When prompted about Microsoft Graph permissions, click **Yes, add them**

**For Access Tokens (API):**
1. Click **Add optional claim** again
2. Select **Access** token type
3. Add the same claims as above
4. This ensures the API receives user information in the access token

#### 6. Create Client Secret (for API)

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Configure:
   - **Description**: `SemanticKernelPoc API Secret`
   - **Expires**: Choose appropriate expiration (6 months, 12 months, or 24 months)
4. Click **Add**
5. **⚠️ IMPORTANT**: Copy the secret **Value** immediately (you won't see it again!)
6. Store this securely - you'll need it for API configuration

#### 7. Configure Manifest (Optional but Recommended)

1. Go to **Manifest**
2. Find `"accessTokenAcceptedVersion"` and ensure it's set to `2`:
   ```json
   "accessTokenAcceptedVersion": 2
   ```
3. This ensures you get v2.0 tokens with proper claims
4. Click **Save**

#### 8. Set Up Azure OpenAI (Recommended)

1. Create **Azure OpenAI** resource in Azure Portal
2. Go to **Azure OpenAI Studio** → **Deployments**
3. Deploy a model:
   - **Model**: `gpt-4` or `gpt-35-turbo` (recommended)
   - **Deployment name**: `gpt-4` (or your preferred name)
   - **Version**: Latest available
4. Note the following for configuration:
   - **Endpoint**: Found in Azure OpenAI resource overview (e.g., `https://your-resource.openai.azure.com/`)
   - **API Key**: Found in Azure OpenAI resource → Keys and Endpoint
   - **Deployment Name**: The name you gave your model deployment

*Alternative: Use OpenAI directly with an API key from [OpenAI](https://platform.openai.com/)*

#### 9. Verify Configuration

**Essential Information to Collect:**
- ✅ **Tenant ID**: Found in Azure AD → Overview
- ✅ **Client ID**: Found in App Registration → Overview
- ✅ **Client Secret**: Created in step 6
- ✅ **Application ID URI**: Created in step 2 (e.g., `api://your-client-id`)
- ✅ **Azure OpenAI Endpoint**: From step 8
- ✅ **Azure OpenAI API Key**: From step 8
- ✅ **Model Deployment Name**: From step 8

**Verification Checklist:**
- [ ] App registration created with correct redirect URIs
- [ ] API exposed with `access_as_user` scope
- [ ] All Microsoft Graph permissions added and admin consent granted
- [ ] Optional claims configured for both ID and Access tokens
- [ ] Client secret created and securely stored
- [ ] Azure OpenAI resource created with model deployed

### ⚙️ Configuration

#### 1. API Configuration

Create `SemanticKernelPoc.Api/appsettings.Development.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "Audience": "api://YOUR_CLIENT_ID"
  },
  "SemanticKernel": {
    "UseAzureOpenAI": true,
    "Endpoint": "https://YOUR_OPENAI_RESOURCE.openai.azure.com/",
    "ApiKey": "YOUR_AZURE_OPENAI_KEY",
    "DeploymentOrModelId": "gpt-4"
  },
  "AllowedOrigins": [
    "http://localhost:5173"
  ],
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Identity": "Information"
    }
  }
}
```

#### 2. Frontend Configuration

Create `SemanticKernelPoc.Web/.env.local`:

```env
VITE_AZURE_CLIENT_ID=YOUR_CLIENT_ID
VITE_AZURE_TENANT_ID=YOUR_TENANT_ID
VITE_API_BASE_URL=https://localhost:7297
```

### 🏃‍♂️ Running the Application

#### 1. Start the API

```bash
cd SemanticKernelPoc.Api
dotnet restore
dotnet run
```

The API will be available at `https://localhost:7297`

#### 2. Start the Frontend

```bash
cd SemanticKernelPoc.Web
npm install
npm run dev
```

The web app will be available at `http://localhost:5173`

#### 3. Test the Application

1. Open `http://localhost:5173` in your browser
2. Click **Sign In** and authenticate with your Microsoft account
3. Try these example queries:

**📅 Calendar Queries:**
- "Show me my calendar events for today"
- "What's my next appointment?"
- "What do I have this week?"
- "Schedule a meeting with John tomorrow at 2 PM"

**📝 Task & Note Queries:**
- "Show me my tasks"
- "What tasks are assigned to me?"
- "Create a note to buy groceries"
- "Show me my recent notes"

**📧 Email Queries:**
- "Show me my recent emails"
- "Send an email to john@example.com about the meeting"
- "Find emails from Sarah"

**🎙️ Meeting Transcript Queries:**
- "Show me recent meeting transcripts"
- "Summarize my last meeting"
- "Extract action items from the meeting"

**🔄 Workflow Triggers:**
- "Create tasks from my last meeting" (Meeting to Tasks workflow)
- "Schedule a project planning meeting" (Project Planning workflow)
- "Send meeting follow-up" (Meeting Follow-up workflow)

## 🔧 Development

### Project Structure

```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/          # .NET 8 Web API
│   ├── Controllers/                # API controllers
│   ├── Models/                     # Data models and workflow definitions
│   ├── Plugins/                    # Semantic Kernel plugins
│   │   ├── Calendar/              # Calendar operations with card support
│   │   ├── Mail/                  # Email operations
│   │   ├── SharePoint/            # SharePoint integration
│   │   ├── OneDrive/              # OneDrive operations
│   │   ├── ToDo/                  # Task/note management with cards
│   │   └── MeetingPlugin.cs       # Meeting transcript analysis
│   └── Services/                  # Business services
│       ├── Graph/                 # Microsoft Graph integration
│       ├── Helpers/               # Utility classes
│       ├── Intelligence/          # Smart function selection
│       ├── Memory/                # Conversation memory
│       └── Workflows/             # Cross-plugin workflow orchestration
├── SemanticKernelPoc.Web/         # React TypeScript frontend
│   ├── src/
│   │   ├── components/            # React components including card renderers
│   │   ├── services/              # API services
│   │   └── types/                 # TypeScript definitions
└── README.md
```

### Key Features Implementation

#### **Smart Function Selection**
- **Location**: `Services/Intelligence/SmartFunctionSelector.cs`
- **Features**: Context-aware function selection, keyword matching, performance optimization
- **Configuration**: Keyword mappings for all plugins and workflow states

#### **Cross-Plugin Workflows**
- **Location**: `Services/Workflows/WorkflowOrchestrator.cs`
- **Features**: Automated workflow detection, step chaining, error handling, retry logic
- **Workflows**: 5 predefined business workflows with dependency management

#### **Card-Based UI**
- **Calendar Cards**: `components/CalendarCard.tsx`
- **Task Cards**: `components/TaskCard.tsx` (via NOTE_CARDS format)
- **Meeting Cards**: `components/MeetingTranscriptCard.tsx`
- **Format**: Special response formats (`CALENDAR_CARDS:`, `NOTE_CARDS:`) for rich displays

#### **Meeting Transcript Analysis**
- **Location**: `Plugins/MeetingPlugin.cs`
- **Features**: Transcript access, AI-powered summaries, decision extraction, task proposal
- **Integration**: Seamless integration with To Do for action item creation

### Adding New Plugins

1. Create a new folder in `SemanticKernelPoc.Api/Plugins/`
2. Add your plugin class inheriting from `BaseGraphPlugin`
3. Create corresponding models in `{PluginName}Models.cs`
4. Register the plugin in `ChatController.cs`
5. Add keyword mappings in `SmartFunctionSelector.cs`
6. Create card components if needed for rich UI display

### Key Classes

- **`BaseGraphPlugin`**: Base class for all Microsoft Graph plugins
- **`IGraphService`**: Service for Microsoft Graph client management
- **`IConversationMemoryService`**: Interface for conversation persistence
- **`ISmartFunctionSelector`**: Interface for intelligent function selection
- **`IWorkflowOrchestrator`**: Interface for cross-plugin workflow management
- **`CalendarHelpers`**: Utility functions for date/time operations

## 🚀 Deployment

### Azure App Service Deployment

1. **Create App Service** in Azure Portal
2. **Configure Application Settings**:
   - Add all settings from `appsettings.json`
   - Set `ASPNETCORE_ENVIRONMENT` to `Production`
3. **Deploy API**: Use Visual Studio, Azure DevOps, or GitHub Actions
4. **Deploy Frontend**: Build and deploy to Azure Static Web Apps or CDN

### Environment Variables for Production

```bash
# Azure AD
AzureAd__TenantId=YOUR_TENANT_ID
AzureAd__ClientId=YOUR_CLIENT_ID
AzureAd__ClientSecret=YOUR_CLIENT_SECRET

# Semantic Kernel
SemanticKernel__UseAzureOpenAI=true
SemanticKernel__Endpoint=YOUR_AZURE_OPENAI_ENDPOINT
SemanticKernel__ApiKey=YOUR_AZURE_OPENAI_KEY
SemanticKernel__DeploymentOrModelId=gpt-4

# CORS
AllowedOrigins__0=https://your-frontend-domain.com
```

## 🔒 Security Considerations

- **Never commit secrets** to version control
- Use **Azure Key Vault** for production secrets
- Implement **proper CORS** configuration
- Validate **user permissions** before Graph API calls
- Use **least privilege** principle for API permissions
- Enable **audit logging** for production environments
- **Workflow security**: Ensure workflows only access user-authorized data
- **Function selection**: Validate selected functions against user permissions

## 🐛 Troubleshooting

### Common Issues

**Azure AD Configuration Errors:**
- **"AADSTS50011: The reply URL specified in the request does not match"**
  - Verify redirect URIs in Azure AD app registration match exactly
  - Check for trailing slashes, http vs https, port numbers

- **"AADSTS65001: The user or administrator has not consented"**
  - Click "Grant admin consent" in API permissions
  - Ensure all permissions show green checkmarks

- **"Invalid audience" or token validation errors**
  - Verify `Audience` in appsettings matches Application ID URI
  - Check `accessTokenAcceptedVersion` is set to `2` in manifest

**Permission Issues:**
- **Meeting transcript access fails**: Ensure `OnlineMeetingTranscript.Read.All` permission is granted
- **Task creation fails**: Verify `Tasks.ReadWrite` permission is granted
- **Calendar access denied**: Check `Calendars.ReadWrite` permission

**Workflow Issues:**
- **Workflows not triggering**: Check keyword mappings in `SmartFunctionSelector.cs`
- **Workflow steps failing**: Review logs for specific plugin errors
- **Function selection issues**: Verify function metadata and descriptions

**Card Display Issues:**
- **Cards not showing**: Ensure functions return proper `CALENDAR_CARDS:` or `NOTE_CARDS:` format
- **Card formatting broken**: Check JSON serialization in response formatters
- **Missing card data**: Verify all required properties are included in card models

### Debug Mode

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.Identity": "Debug",
      "SemanticKernelPoc": "Debug"
    }
  }
}
```

## 📚 Documentation

- **[Cross-Plugin Workflows Guide](CROSS_PLUGIN_WORKFLOWS.md)** - Detailed workflow documentation
- **[Smart Function Selection](Services/Intelligence/SmartFunctionSelector.cs)** - Function selection algorithm
- **[Plugin Development Guide](Plugins/README.md)** - Creating new plugins
- **[Card UI Components](SemanticKernelPoc.Web/src/components/)** - Frontend card components

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Microsoft Semantic Kernel team
- Microsoft Graph API team
- Azure OpenAI service
- React and TypeScript communities 