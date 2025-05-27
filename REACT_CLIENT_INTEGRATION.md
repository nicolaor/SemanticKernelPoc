# React Client Integration

## Overview

The SemanticKernelPoc solution now includes a complete React client application that runs on port 5173, integrated with the development workflow automation scripts.

## Project Structure

```
SemanticKernelPoc/
├── SemanticKernelPoc.Api/          # ASP.NET Core Web API (ports 5040/7002)
├── SemanticKernelPoc.McpServer/    # MCP Server (stdio-based)
├── SemanticKernelPoc.Web/          # React Client (port 5173)
│   ├── src/                        # React source code
│   ├── package.json                # npm dependencies
│   ├── vite.config.ts              # Vite configuration (port 5173)
│   ├── Dockerfile                  # Docker configuration
│   └── .dockerignore               # Docker ignore file
├── start-all.sh                    # Start all services (macOS/Linux)
├── stop-all.sh                     # Stop all services (macOS/Linux)
├── status.sh                       # Check service status (macOS/Linux)
├── get-ports.sh                    # Port configuration utility (macOS/Linux)
├── start-all.ps1                   # Start all services (Windows)
├── get-ports.ps1                   # Port configuration utility (Windows)
└── docker-compose.yml              # Docker Compose configuration
```

## Technology Stack

### React Client
- **React 19.1.0** - Latest React version
- **TypeScript** - Type-safe development
- **Vite** - Fast build tool and dev server
- **Tailwind CSS** - Utility-first CSS framework
- **ESLint** - Code linting and formatting

### Port Configuration
- **React Client**: `https://localhost:31337`
- **API HTTPS**: `https://localhost:7002`
- **API HTTP**: `http://localhost:5040`
- **Swagger UI**: `https://localhost:7002/swagger`

## Development Workflow

### Quick Start (Recommended)
```bash
# Start all services (MCP Server + API + React Client)
./start-all.sh

# Check status
./status.sh

# Stop all services
./stop-all.sh
```

### Windows Users
```powershell
# Start all services
.\start-all.ps1

# Check configuration
.\get-ports.ps1
```

### Manual Start (Alternative)
```bash
# Terminal 1: MCP Server
cd SemanticKernelPoc.McpServer
dotnet run

# Terminal 2: API Server
cd SemanticKernelPoc.Api
dotnet run

# Terminal 3: React Client
cd SemanticKernelPoc.Web
npm run dev
```

## Features

### Automated Development Scripts
- **Port Management**: Automatically detects and uses configured ports
- **Process Cleanup**: Kills existing processes before starting new ones
- **Logging**: Centralized logs in `logs/` directory
- **Status Monitoring**: Real-time service status and port usage
- **Cross-Platform**: Shell scripts for macOS/Linux, PowerShell for Windows

### Docker Support
- **Multi-Service**: Docker Compose with all three services
- **Health Checks**: Automatic service health monitoring
- **Development Mode**: Hot reload for React client
- **Dependency Management**: Proper service startup order

### Configuration Management
- **Vite Config**: Explicit port 5173 configuration with host binding
- **CORS**: API configured to allow React client origin
- **Environment**: Development-optimized settings

## Service Integration

### API ↔ React Client
The API is configured to accept requests from the React client:

```json
// appsettings.json
{
  "AllowedOrigins": [
            "https://localhost:31337",
    "http://localhost:5174"
  ]
}
```

### MCP Server ↔ API
The API communicates with the MCP Server for SharePoint search functionality through the SharePoint MCP Plugin.

## Logging and Monitoring

### Log Files
- `logs/mcp-server.log` - MCP Server output
- `logs/api-server.log` - API Server output  
- `logs/client.log` - React Client output

### Real-Time Monitoring
```bash
# Watch all logs simultaneously
tail -f logs/mcp-server.log logs/api-server.log logs/client.log

# Individual service logs
tail -f logs/client.log
```

### Service Status
```bash
./status.sh
```
Shows:
- Process status (running/stopped)
- Port usage (in use/available)
- Recent log entries
- Quick access commands

## Docker Deployment

### Build and Run
```bash
# Build and start all services
docker-compose up --build

# Run in background
docker-compose up -d

# Stop services
docker-compose down
```

### Service URLs (Docker)
- React Client: `https://localhost:31337`
- API: `http://localhost:5040` / `https://localhost:7002`
- Swagger: `https://localhost:7002/swagger`

## Entra ID Configuration

Ensure your Azure Entra ID app registration includes these redirect URIs:
- `https://localhost:7002/signin-oidc`
- `http://localhost:5040/signin-oidc`

Use `./get-ports.sh` or `.\get-ports.ps1` to verify current configuration.

## Troubleshooting

### Port Conflicts
```bash
# Check what's using a port
lsof -i :31337

# Kill process on port
./stop-all.sh  # Handles cleanup automatically
```

### Service Not Starting
```bash
# Check logs
./status.sh

# View detailed logs
tail -f logs/client.log
```

### React Client Issues
```bash
# Reinstall dependencies
cd SemanticKernelPoc.Web
npm ci

# Clear cache
npm run build
```

## Development Benefits

1. **Single Command Startup**: No need to manage 3 terminals manually
2. **Automatic Port Management**: Uses actual configured ports, not hardcoded defaults
3. **Process Cleanup**: Prevents port conflicts and zombie processes
4. **Centralized Logging**: All service logs in one place
5. **Status Monitoring**: Quick overview of all services
6. **Cross-Platform**: Works on macOS, Linux, and Windows
7. **Docker Ready**: Full containerization support
8. **Hot Reload**: React client updates automatically during development

## Next Steps

1. **Frontend Development**: Build React components to interact with the API
2. **Authentication**: Implement Azure AD authentication in React client
3. **SharePoint Integration**: Create UI for SharePoint search functionality
4. **Testing**: Add automated tests for the React client
5. **Production Build**: Configure production deployment pipeline 