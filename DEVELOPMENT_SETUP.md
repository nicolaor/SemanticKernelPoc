# Development Setup Guide

This guide provides multiple ways to easily start and stop all SemanticKernelPoc services without manually managing multiple terminals.

## 🚀 Quick Start Options

### Option 1: Shell Scripts (Recommended for macOS/Linux)

The simplest way to manage all services:

```bash
# Start all services
./start-all.sh

# Check status
./status.sh

# Stop all services
./stop-all.sh
```

**Features:**
- ✅ Starts both MCP Server and API Server in background
- ✅ Automatic port cleanup (kills existing processes)
- ✅ Centralized logging in `logs/` directory
- ✅ PID tracking for clean shutdown
- ✅ Status monitoring

### Option 2: PowerShell Scripts (Windows)

For Windows users:

```powershell
# Start all services
.\start-all.ps1

# Stop all services
.\stop-all.ps1
```

### Option 3: Docker Compose (Production-like)

For containerized development:

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Option 4: Manual .NET Commands

If you prefer manual control:

```bash
# Terminal 1: MCP Server
cd SemanticKernelPoc.McpServer
dotnet run

# Terminal 2: API Server
cd SemanticKernelPoc.Api
dotnet run
```

## 📊 Service Information

### Services Started:
1. **MCP Server** (SemanticKernelPoc.McpServer)
   - Handles SharePoint CoffeeNet site search
   - Uses Model Context Protocol (MCP)
   - Runs as background process

2. **API Server** (SemanticKernelPoc.Api)
   - Main ASP.NET Core Web API
   - Semantic Kernel integration
   - Swagger UI available
   - URLs: `http://localhost:5000` and `https://localhost:5001`

### Ports Used:
- **5000**: API HTTP
- **5001**: API HTTPS
- **8080**: MCP Server (Docker only)

## 📝 Logging

All scripts create centralized logs in the `logs/` directory:

```bash
# View real-time logs
tail -f logs/mcp-server.log     # MCP Server logs
tail -f logs/api-server.log     # API Server logs

# Windows PowerShell
Get-Content logs\mcp-server.log -Wait
Get-Content logs\api-server.log -Wait
```

## 🔧 Script Details

### start-all.sh Features:
- **Port Cleanup**: Automatically kills processes on ports 5000/5001
- **Background Execution**: Services run in background with output redirected to logs
- **PID Tracking**: Saves process IDs for clean shutdown
- **Status Reporting**: Shows service status and useful commands
- **Error Handling**: Graceful handling of startup issues

### stop-all.sh Features:
- **Graceful Shutdown**: Attempts normal termination first
- **Force Kill**: Falls back to force kill if needed
- **Port Cleanup**: Ensures ports are freed
- **PID Cleanup**: Removes PID files
- **Process Cleanup**: Kills any remaining project processes

### status.sh Features:
- **Service Status**: Shows if services are running
- **Port Status**: Checks if ports are in use
- **Recent Logs**: Displays last few log entries
- **Quick Commands**: Shows available management commands

## 🐳 Docker Compose Details

The `docker-compose.yml` provides:
- **Service Dependencies**: API waits for MCP server to be healthy
- **Health Checks**: Automatic service health monitoring
- **Volume Mounts**: Configuration files mounted from host
- **Network Isolation**: Services communicate via Docker network
- **Restart Policies**: Automatic restart on failure

## 🎯 Usage Examples

### Development Workflow:
```bash
# Start development environment
./start-all.sh

# Check if everything is running
./status.sh

# View API documentation
open https://localhost:5001/swagger

# Monitor logs during development
tail -f logs/api-server.log

# Stop when done
./stop-all.sh
```

### Debugging Workflow:
```bash
# Start only MCP server in background
cd SemanticKernelPoc.McpServer && dotnet run > ../logs/mcp-server.log 2>&1 &

# Start API in foreground for debugging
cd SemanticKernelPoc.Api && dotnet run
```

### Production-like Testing:
```bash
# Use Docker Compose
docker-compose up -d

# Check service health
docker-compose ps

# View aggregated logs
docker-compose logs -f

# Scale services (if needed)
docker-compose up -d --scale api-server=2
```

## 🛠️ Troubleshooting

### Common Issues:

1. **Port Already in Use**:
   ```bash
   # Kill processes on specific ports
   lsof -ti:5000 | xargs kill -9
   lsof -ti:5001 | xargs kill -9
   ```

2. **Services Won't Start**:
   ```bash
   # Check logs for errors
   cat logs/mcp-server.log
   cat logs/api-server.log
   
   # Verify .NET installation
   dotnet --version
   ```

3. **MCP Server Not Responding**:
   ```bash
   # Test MCP server directly
   echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run --project SemanticKernelPoc.McpServer
   ```

4. **API Can't Connect to MCP**:
   - Ensure MCP server starts before API
   - Check MCP server logs for errors
   - Verify MCP client configuration in API

### Log Locations:
- **Shell Scripts**: `logs/mcp-server.log`, `logs/api-server.log`
- **Docker**: `docker-compose logs [service-name]`
- **Manual**: Console output

## 🔄 Advanced Usage

### Custom Environment Variables:
```bash
# Set custom environment before starting
export ASPNETCORE_ENVIRONMENT=Staging
./start-all.sh
```

### Development with Hot Reload:
```bash
# Start MCP server normally
./start-all.sh

# Stop API and run with hot reload
pkill -f "SemanticKernelPoc.Api"
cd SemanticKernelPoc.Api && dotnet watch run
```

### Multiple Environments:
```bash
# Create environment-specific scripts
cp start-all.sh start-staging.sh
# Edit start-staging.sh to set ASPNETCORE_ENVIRONMENT=Staging
```

## 📋 Checklist

Before starting development:
- [ ] .NET 8.0 SDK installed
- [ ] All NuGet packages restored (`dotnet restore`)
- [ ] Configuration files properly set up
- [ ] Ports 5000/5001 available
- [ ] Scripts are executable (`chmod +x *.sh`)

## 🎉 Quick Commands Reference

```bash
# Essential commands
./start-all.sh          # Start everything
./status.sh             # Check status
./stop-all.sh           # Stop everything

# Monitoring
tail -f logs/*.log      # Watch all logs
./status.sh             # Service status

# Docker alternative
docker-compose up -d    # Start with Docker
docker-compose down     # Stop Docker services

# Manual control
dotnet run --project SemanticKernelPoc.McpServer    # MCP only
dotnet run --project SemanticKernelPoc.Api          # API only
```

This setup eliminates the need for multiple terminals and provides a professional development experience! 🚀 