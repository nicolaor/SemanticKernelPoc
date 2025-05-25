# 🤖 Semantic Kernel PoC - Microsoft 365 AI Assistant

A comprehensive proof-of-concept application demonstrating the integration of **Microsoft Semantic Kernel** with **Microsoft Graph API** to create an intelligent AI assistant for Microsoft 365 productivity tasks.

## 📋 Overview

This application provides a conversational AI interface that can interact with your Microsoft 365 data through natural language. The AI assistant can help you manage your calendar, emails, SharePoint files, OneDrive content, and create notes using Microsoft To Do.

### 🌟 Key Features

- **🗓️ Calendar Management**: View, create, update, and delete calendar events
- **📧 Email Operations**: Read, send, search emails and manage drafts
- **📁 SharePoint Integration**: Browse sites, search files, and access documents
- **💾 OneDrive Access**: View drive information and manage files
- **📝 Note Taking**: Create and manage notes using Microsoft To Do
- **💬 Conversation Memory**: Maintains context across chat sessions
- **🎨 Modern UI**: Clean, responsive React TypeScript frontend
- **🔒 Secure Authentication**: Azure AD integration with proper scoping

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

**Delegated Permissions:**
- `User.Read` - Read user profile
- `Calendars.ReadWrite` - Full calendar access
- `Mail.ReadWrite` - Read and write mail
- `Mail.Send` - Send mail
- `Sites.Read.All` - Read SharePoint sites
- `Files.Read.All` - Read OneDrive files
- `Tasks.ReadWrite` - Read and write tasks (for notes)

**Steps:**
1. Go to **API permissions** → **Add a permission**
2. Select **Microsoft Graph** → **Delegated permissions**
3. Add each permission listed above
4. **Important**: Click **Grant admin consent for [Your Organization]** (requires admin privileges)
5. Verify all permissions show "Granted for [Your Organization]" with green checkmarks

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

**Important Notes:**
- The `name` claim provides the user's display name (e.g., "John Doe")
- The `preferred_username` claim usually contains the user's email
- The `given_name` and `family_name` claims provide first and last names separately
- These claims are essential for the application to display user information correctly

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

**Configuration Values Explained:**
- `TenantId`: Your Azure AD tenant ID (from Azure AD → Overview)
- `ClientId`: Your app registration client ID
- `ClientSecret`: The secret value you created and copied
- `Audience`: Should match your Application ID URI (usually `api://YOUR_CLIENT_ID`)
- `Endpoint`: Your Azure OpenAI resource endpoint
- `ApiKey`: Your Azure OpenAI API key
- `DeploymentOrModelId`: The name of your deployed model

**For OpenAI (instead of Azure OpenAI):**
```json
"SemanticKernel": {
  "UseAzureOpenAI": false,
  "ApiKey": "YOUR_OPENAI_API_KEY",
  "DeploymentOrModelId": "gpt-4"
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
3. **Verify Authentication**: Check that your name and email appear in the UI
   - Look for your display name in the top-right corner or user profile area
   - If name shows as "User" or is missing, check the claims configuration
   - Verify that the `name` and `preferred_username` claims are properly configured
4. **Test API Access**: Try asking questions like:
   - "What's on my calendar today?"
   - "Send an email to john@example.com about the meeting"
   - "Create a note to buy groceries"
   - "Show me my recent emails"

**If authentication fails:**
- Check browser developer console for errors
- Verify all Azure AD configuration steps were completed
- Ensure the user account has Microsoft 365 licenses
- Try signing out and signing in again

**If API calls fail:**
- Check the API logs for detailed error messages
- Verify Microsoft Graph permissions are granted
- Ensure the user has access to the requested resources (calendar, mail, etc.)

## 🔧 Development

### Project Structure

```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/          # .NET 8 Web API
│   ├── Controllers/                # API controllers
│   ├── Models/                     # Data models
│   ├── Plugins/                    # Semantic Kernel plugins
│   │   ├── Calendar/              # Calendar operations
│   │   ├── Mail/                  # Email operations
│   │   ├── SharePoint/            # SharePoint integration
│   │   ├── OneDrive/              # OneDrive operations
│   │   └── ToDo/                  # Note-taking via To Do
│   └── Services/                  # Business services
│       ├── Graph/                 # Microsoft Graph integration
│       ├── Helpers/               # Utility classes
│       └── Memory/                # Conversation memory
├── SemanticKernelPoc.Web/         # React TypeScript frontend
│   ├── src/
│   │   ├── components/            # React components
│   │   ├── services/              # API services
│   │   └── types/                 # TypeScript definitions
└── README.md
```

### Adding New Plugins

1. Create a new folder in `SemanticKernelPoc.Api/Plugins/`
2. Add your plugin class inheriting from `BaseGraphPlugin`
3. Create corresponding models in `{PluginName}Models.cs`
4. Register the plugin in `ChatController.cs`

### Key Classes

- **`BaseGraphPlugin`**: Base class for all Microsoft Graph plugins
- **`IGraphService`**: Service for Microsoft Graph client management
- **`IConversationMemoryService`**: Interface for conversation persistence
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

## 🐛 Troubleshooting

### Common Issues

**Azure AD Configuration Errors:**
- **"AADSTS50011: The reply URL specified in the request does not match"**
  - Verify redirect URIs in Azure AD app registration match exactly
  - Check for trailing slashes, http vs https, port numbers
  - Ensure both development (`http://localhost:5173`) and production URLs are added

- **"AADSTS65001: The user or administrator has not consented"**
  - Click "Grant admin consent" in API permissions
  - Ensure all permissions show green checkmarks
  - User may need to sign out and sign in again after consent

- **"AADSTS50105: The signed in user is not assigned to a role"**
  - Add users to the application in Azure AD → Enterprise Applications
  - Or disable user assignment requirement in Enterprise Applications → Properties

- **"Invalid audience" or token validation errors**
  - Verify `Audience` in appsettings matches Application ID URI
  - Check `accessTokenAcceptedVersion` is set to `2` in manifest
  - Ensure API is properly exposed with correct scope

- **Missing user claims (name, email, etc.)**
  - Configure optional claims in Token configuration
  - Add claims for both ID tokens and Access tokens
  - Verify Microsoft Graph permissions are granted
  - Check that `name`, `preferred_username`, `email`, `given_name`, and `family_name` claims are added
  - If user name still doesn't appear, check browser developer tools → Network → look for the token and verify claims are present
  - Try signing out and signing in again after adding claims

**Authentication Errors:**
- Verify Azure AD app registration configuration
- Check redirect URIs match exactly
- Ensure admin consent is granted for API permissions

**Graph API Errors:**
- Verify user has necessary licenses (Microsoft 365)
- Check API permissions are granted and consented
- Ensure user is in the correct tenant

**Build Errors:**
- Ensure .NET 8 SDK is installed
- Run `dotnet restore` in the API project
- Run `npm install` in the Web project

### Debug Mode

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Graph": "Debug",
      "SemanticKernelPoc": "Debug"
    }
  }
}
```

## 📚 Additional Resources

- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel/)
- [Microsoft Graph API Documentation](https://docs.microsoft.com/graph/)
- [Azure AD App Registration Guide](https://docs.microsoft.com/azure/active-directory/develop/quickstart-register-app)
- [Microsoft 365 Developer Program](https://developer.microsoft.com/microsoft-365/dev-program)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙋‍♂️ Support

For questions and support:
- Create an issue in this repository
- Check the troubleshooting section above
- Review Microsoft documentation links

---

**Happy coding! 🚀** 