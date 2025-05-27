# 🛠️ Development Environment Setup Guide

This guide provides step-by-step instructions for setting up the SemanticKernelPoc application for local development.

## 📋 Prerequisites

### Required Software
- **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** and npm - [Download here](https://nodejs.org/)
- **Git** - [Download here](https://git-scm.com/)
- **Visual Studio Code** or **Visual Studio 2022** (recommended)

### Required Services
- **Azure AD Tenant** with admin access
- **Azure OpenAI Service** or **OpenAI API** account
- **Microsoft 365 Tenant** (for testing Graph API integration)

## 🔐 Azure AD App Registration Setup

### 1. Create App Registration

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure:
   - **Name**: `SemanticKernelPoc`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: `Web` - `https://localhost:31337`

### 2. Configure Authentication

1. Go to **Authentication** in your app registration
2. Add these redirect URIs:
   - `https://localhost:31337`
   - `https://localhost:31338/signin-oidc`
3. Under **Implicit grant and hybrid flows**:
   - ✅ Check **ID tokens**
   - ✅ Check **Access tokens**
4. Set **Logout URL**: `https://localhost:31337`

### 3. Configure API Permissions

1. Go to **API permissions**
2. Click **Add a permission** > **Microsoft Graph** > **Delegated permissions**
3. Add these permissions:
   - `User.Read` (Basic profile)
   - `Mail.ReadWrite` (Email access)
   - `Mail.Send` (Send emails)
   - `Calendars.ReadWrite` (Calendar access)
   - `Calendars.ReadWrite.Shared` (Shared calendars)
   - `Files.ReadWrite.All` (OneDrive access)
   - `Sites.Read.All` (SharePoint read access)
   - `Tasks.ReadWrite` (To Do tasks)
4. Click **Grant admin consent** for your organization

### 4. Create Client Secret

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Set description: `Development Secret`
4. Set expiration: `24 months`
5. **⚠️ IMPORTANT**: Copy the secret value immediately - you won't see it again!

### 5. Configure API Exposure (for MCP Service)

1. Go to **Expose an API**
2. Click **Set** next to Application ID URI
3. Accept the default: `api://your-client-id`
4. Click **Add a scope**:
   - **Scope name**: `access_as_user`
   - **Who can consent**: `Admins and users`
   - **Admin consent display name**: `Access as user`
   - **Admin consent description**: `Allow the application to access the API as the signed-in user`
   - **User consent display name**: `Access as user`
   - **User consent description**: `Allow the application to access the API as you`

## 🤖 AI Service Setup

### Option A: Azure OpenAI (Recommended)

1. Create an **Azure OpenAI** resource in Azure Portal
2. Deploy a model (e.g., `gpt-4o-mini` or `gpt-4o`)
3. Note down:
   - **Endpoint**: `https://your-resource.openai.azure.com/`
   - **API Key**: From the resource's **Keys and Endpoint** section
   - **Deployment Name**: The name you gave your model deployment

### Option B: OpenAI API

1. Create an account at [OpenAI](https://platform.openai.com/)
2. Generate an API key from the **API Keys** section
3. Note down:
   - **API Key**: `sk-...`
   - **Model**: `gpt-4o-mini` or `gpt-4o`

## 📁 Project Setup

### 1. Clone Repository

```bash
git clone <repository-url>
cd SemanticKernelPoc
```

### 2. Install Dependencies

```bash
# Install .NET dependencies
dotnet restore

# Install Node.js dependencies
cd SemanticKernelPoc.Web
npm install
cd ..
```

### 3. Configure Development Settings

#### API Configuration

Create or update `SemanticKernelPoc.Api/appsettings.Development.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "CallbackPath": "/signin-oidc",
    "Scopes": "access_as_user https://graph.microsoft.com/.default",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
    "Audience": "api://YOUR_CLIENT_ID_HERE"
  },
  "SemanticKernel": {
    "DeploymentOrModelId": "gpt-4o-mini",
    "Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT_OR_EMPTY_FOR_OPENAI",
    "ApiKey": "YOUR_API_KEY_HERE",
    "UseAzureOpenAI": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Identity": "Debug"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": [
    "https://localhost:31337"
  ],
  "McpServer": {
    "Url": "https://localhost:31339"
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:31338",
        "Certificate": {
          "Path": "../certs/localhost.crt",
          "KeyPath": "../certs/localhost.key"
        }
      }
    }
  }
}
```

#### MCP Server Configuration

Create or update `SemanticKernelPoc.McpServer/appsettings.Development.json`:

```json
{
  "AzureAd": {
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**Replace the following placeholders:**
- `YOUR_TENANT_ID_HERE`: Your Azure AD tenant ID
- `YOUR_CLIENT_ID_HERE`: Your app registration client ID
- `YOUR_CLIENT_SECRET_HERE`: The client secret you created
- `YOUR_API_KEY_HERE`: Your OpenAI or Azure OpenAI API key
- `YOUR_AZURE_OPENAI_ENDPOINT_OR_EMPTY_FOR_OPENAI`: Your Azure OpenAI endpoint (leave empty for OpenAI)

### 4. HTTPS Certificate Setup

The application requires HTTPS certificates for secure communication between services.

#### Automatic Certificate Generation

The startup script will automatically generate certificates if they don't exist:

```bash
./start-all.sh
```

#### Manual Certificate Setup

If you need to manually create certificates:

```bash
# Create certificates directory
mkdir -p certs

# Generate certificate and key
openssl req -x509 -newkey rsa:4096 -keyout certs/localhost.key -out certs/localhost.crt -days 365 -nodes -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

# Create PFX file (optional)
openssl pkcs12 -export -out certs/localhost.pfx -inkey certs/localhost.key -in certs/localhost.crt -passout pass:
```

#### Trust the Certificate

**macOS:**
```bash
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/localhost.crt
```

**Windows (PowerShell as Administrator):**
```powershell
Import-Certificate -FilePath "certs\localhost.crt" -CertStoreLocation Cert:\LocalMachine\Root
```

**Linux:**
```bash
sudo cp certs/localhost.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates
```

## 🚀 Running the Application

### Start All Services

```bash
./start-all.sh
```

This will start:
- **MCP Server** on `https://localhost:31339`
- **API Server** on `https://localhost:31338`
- **React Client** on `https://localhost:31337`

### Individual Service Management

**MCP Server:**
```bash
cd SemanticKernelPoc.McpServer
dotnet run --environment=Development
```

**API Server:**
```bash
cd SemanticKernelPoc.Api
dotnet run --environment=Development
```

**React Client:**
```bash
cd SemanticKernelPoc.Web
npm run dev
```

### Stop All Services

```bash
./stop-all.sh
```

## 🔍 Verification

### 1. Check Service Health

Visit these URLs to verify services are running:
- **React App**: https://localhost:31337
- **API Health**: https://localhost:31338/health
- **API Swagger**: https://localhost:31338/swagger
- **MCP Server**: https://localhost:31339 (should return 406 Method Not Allowed)

### 2. Test Authentication

1. Open https://localhost:31337
2. Click **Sign In**
3. Complete Azure AD authentication
4. Verify you can see the chat interface

### 3. Test AI Integration

1. Type a simple message like "Hello"
2. Verify you get an AI response
3. Try Microsoft 365 commands like:
   - "Show me my emails"
   - "What's on my calendar today?"
   - "Show me my tasks"

## 📝 Monitoring and Debugging

### Log Files

Logs are automatically created in the `logs/` directory:
- `logs/mcp-server.log` - MCP server logs
- `logs/api-server.log` - API server logs
- `logs/client.log` - React client logs

### Real-time Log Monitoring

```bash
# Monitor all logs
tail -f logs/*.log

# Monitor specific service
tail -f logs/api-server.log
```

### Common Issues

#### SSL Certificate Issues
- **Problem**: Browser shows security warnings
- **Solution**: Trust the certificate as described above

#### Authentication Issues
- **Problem**: Login fails or returns errors
- **Solution**: Verify Azure AD configuration and redirect URIs

#### API Connection Issues
- **Problem**: Frontend can't connect to API
- **Solution**: Check that all services are running and certificates are trusted

#### Graph API Permission Issues
- **Problem**: "Insufficient privileges" errors
- **Solution**: Ensure admin consent was granted for all required permissions

## 🔧 Development Tips

### IDE Configuration

**Visual Studio Code Extensions:**
- C# Dev Kit
- Azure Account
- REST Client
- Tailwind CSS IntelliSense

**Visual Studio 2022:**
- Ensure ASP.NET and web development workload is installed

### Environment Variables

You can also use environment variables instead of configuration files:

```bash
export AzureAd__TenantId="your-tenant-id"
export AzureAd__ClientId="your-client-id"
export AzureAd__ClientSecret="your-client-secret"
export SemanticKernel__ApiKey="your-api-key"
```

### Hot Reload

- **.NET**: Automatic hot reload is enabled in development
- **React**: Vite provides instant hot module replacement

## 🛡️ Security Considerations

### Development vs Production

- **Development**: Uses self-signed certificates and relaxed security settings
- **Production**: Requires proper SSL certificates and enhanced security configuration

### Secret Management

- **Development**: Secrets in `appsettings.Development.json` (git-ignored)
- **Production**: Use Azure Key Vault or similar secure secret storage

### CORS Configuration

Development CORS is configured to allow `https://localhost:31337`. Update for production domains.

## 📚 Additional Resources

- [Microsoft Graph API Documentation](https://docs.microsoft.com/en-us/graph/)
- [Azure AD App Registration Guide](https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)

## 🆘 Getting Help

If you encounter issues:

1. Check the logs in the `logs/` directory
2. Verify all prerequisites are installed
3. Ensure Azure AD configuration is correct
4. Confirm certificates are trusted
5. Check that all required permissions are granted

For specific errors, include relevant log entries when seeking help. 