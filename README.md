# 🤖 Semantic Kernel PoC - Microsoft 365 AI Assistant

A comprehensive proof-of-concept application demonstrating the integration of **Microsoft Semantic Kernel** with **Microsoft Graph API** and **Model Context Protocol (MCP)** to create an intelligent AI assistant for Microsoft 365 productivity tasks.

## 📋 Overview

This application provides a conversational AI interface that can interact with your Microsoft 365 data through natural language. The AI assistant can help you manage your calendar, emails, SharePoint sites, OneDrive content, and create notes using Microsoft To Do. The application features a **SharePoint MCP service** that provides secure, authenticated access to SharePoint search capabilities.

## 🏗️ Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│    React    │────►│  ASP.NET    │────►│    MCP      │
│   Client    │     │    API      │     │  Service    │
└─────────────┘     └─────────────┘     └─────────────┘
                           │                     │
                           ▼                     ▼
                    ┌─────────────────────────────────┐
                    │     Microsoft 365 Services     │
                    └─────────────────────────────────┘
```

The application consists of three main software components:

**React Client**: Modern web interface providing chat functionality and user authentication

**ASP.NET Core API**: Backend service powered by Semantic Kernel that orchestrates AI responses and integrates with Microsoft 365 services

**MCP Service**: Specialized microservice handling SharePoint operations through the Model Context Protocol

**External Integration**: Microsoft 365 ecosystem providing authentication and productivity services

## ✨ Key Features

### 🤖 **Intelligent Conversational Interface**
- Natural language processing using Azure OpenAI
- Context-aware responses with conversation memory
- Multi-turn conversations with session management

### 🔗 **Microsoft 365 Integration**
- **📧 Email Management**: Read, search, and compose emails
- **📅 Calendar Operations**: View events, schedule meetings, check availability  
- **📁 OneDrive Access**: Browse and manage files
- **📝 Task Management**: Create and manage To Do items
- **🔍 SharePoint Search**: Find sites and content across the tenant

### 🛡️ **Enterprise Security**
- Azure AD authentication with secure token handling
- On-behalf-of token flow for service-to-service calls
- Dynamic tenant discovery for multi-tenant support

### 🔌 **Extensible Architecture**
- Model Context Protocol (MCP) for modular functionality
- Plugin-based system for easy feature addition
- Semantic Kernel integration for AI orchestration

## 🎯 Use Cases

- **📊 Productivity Assistant**: "Show me my meetings for this week and create a summary"
- **📂 Content Discovery**: "Find recent SharePoint sites related to our project"
- **📧 Email Management**: "Draft an email to the team about tomorrow's meeting"
- **📅 Calendar Coordination**: "When is my next available 30-minute slot this week?"
- **📝 Task Organization**: "Create a to-do item for reviewing the quarterly report"

## 🛠️ Technology Stack

### **Frontend**
- **React 18** with TypeScript
- **Vite** for fast development and building
- **Tailwind CSS** for modern, responsive styling
- **Real-time updates** via Server-Sent Events (SSE)

### **Backend**
- **ASP.NET Core 8.0** Web API
- **Microsoft Semantic Kernel** for AI orchestration
- **Microsoft Graph SDK** for Microsoft 365 integration
- **Microsoft Identity Platform** for authentication

### **MCP Service**
- **Model Context Protocol** for modular architecture
- **SharePoint REST API** integration
- **Dynamic tenant discovery** via Microsoft Graph
- **JWT token handling** for secure authentication

### **AI & Integration**
- **Azure OpenAI** for natural language processing
- **Microsoft Graph API** for Microsoft 365 data access
- **Azure Active Directory** for identity management

## 🚀 Getting Started

### Quick Start

For a complete development environment setup, see **[DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md)** for detailed instructions.

### Prerequisites

- **.NET 8.0 SDK**
- **Node.js 18+** and npm
- **Azure AD App Registration** with appropriate permissions
- **Azure OpenAI** service endpoint and API key

### Quick Setup

1. **Clone and install dependencies**:
   ```bash
   git clone <repository-url>
   cd SemanticKernelPoc
   dotnet restore
   cd SemanticKernelPoc.Web && npm install && cd ..
   ```

2. **Configure secrets** (see [DEVELOPMENT-SETUP.md](DEVELOPMENT-SETUP.md) for details):
   - Copy `SemanticKernelPoc.Api/appsettings.Development.template.json` to `appsettings.Development.json`
   - Fill in your Azure AD and OpenAI credentials

3. **Setup HTTPS certificates**:
   ```bash
   # The start script will generate certificates automatically
   ./start-all.sh
   
   # Trust the certificate (macOS example)
   sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/localhost.crt
   ```

4. **Access the application**:
   - **React App**: https://localhost:31337
   - **API Documentation**: https://localhost:31338/swagger

### Configuration Files

The application uses the following configuration approach:

- **`appsettings.json`**: Base configuration with placeholders (committed to git)
- **`appsettings.Development.json`**: Development secrets (git-ignored, contains real credentials)
- **`appsettings.Development.template.json`**: Template for developers (committed to git)

**🔒 Security Note**: Development configuration files containing secrets are automatically excluded from git via `.gitignore`. Never commit real secrets to version control.

## 📝 Logs and Monitoring

The startup script creates organized log files:
- **MCP Service**: `logs/mcp-server.log`
- **API Service**: `logs/api-server.log`  
- **React Client**: `logs/client.log`

Monitor logs in real-time:
```bash
tail -f logs/mcp-server.log
tail -f logs/api-server.log
tail -f logs/client.log
```

## 🔧 Configuration

### Core Settings
- **Authentication**: Configure Azure AD settings for your tenant
- **AI Model**: Set up Azure OpenAI endpoint and deployment
- **Logging**: Adjust log levels in `appsettings.json`
- **CORS**: Configure allowed origins for the React client

### MCP Service Configuration
The MCP service automatically discovers tenant information and configures SharePoint access dynamically, requiring no static tenant-specific configuration.

## 🏃‍♂️ Development

### Project Structure
```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/          # ASP.NET Core Web API
├── SemanticKernelPoc.McpServer/    # Model Context Protocol Service
├── SemanticKernelPoc.Web/          # React Frontend
├── certs/                          # HTTPS certificates for development
│   ├── localhost.crt               # Certificate file
│   ├── localhost.key               # Private key
│   └── localhost.pfx               # PKCS#12 bundle for .NET
├── start-all.sh                    # Service startup script
├── stop-all.sh                     # Service shutdown script
└── README.md                       # This documentation
```

### Certificate Information
The `certs/` directory contains self-signed certificates for HTTPS development:
- **localhost.crt**: X.509 certificate for localhost
- **localhost.key**: Private key (unencrypted)
- **localhost.pfx**: PKCS#12 bundle for .NET applications (no password)

These certificates are valid for 365 days and include Subject Alternative Names for both `localhost` and `127.0.0.1`.

### Adding New Features
1. **Create new plugins** in the API project
2. **Extend MCP tools** for additional Microsoft 365 services
3. **Add UI components** in the React client
4. **Configure permissions** in Azure AD as needed

## 🤝 Contributing

This is a proof-of-concept project demonstrating the integration of modern AI capabilities with Microsoft 365 productivity tools. Feel free to extend and adapt it for your specific use cases.

## 📄 License

This project is for demonstration and educational purposes. 