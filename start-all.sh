#!/bin/bash

# Start All SemanticKernelPoc Services
echo "🚀 Starting SemanticKernelPoc services..."

# Define ports
CLIENT_PORT=31337
API_PORT=31338
MCP_PORT=31339

echo "📋 Using HTTPS ports: Client=$CLIENT_PORT, API=$API_PORT, MCP=$MCP_PORT"

# Create logs directory if it doesn't exist
mkdir -p logs

# Function to clean log files
clean_logs() {
    echo "🧹 Cleaning up old log files..."
    rm -f logs/*.log
    rm -f logs/*.pid
    echo "✅ Log files cleaned"
}

# Function to check if a port is in use
check_port() {
    lsof -ti:$1 > /dev/null 2>&1
}

# Function to kill process on port
kill_port() {
    if check_port $1; then
        echo "⚠️  Port $1 is in use, killing existing process..."
        lsof -ti:$1 | xargs kill -9 2>/dev/null || true
        sleep 2
    fi
}

# Function to check build result and handle errors
check_build_result() {
    local project_name=$1
    local build_result=$2
    
    if [ $build_result -ne 0 ]; then
        echo ""
        echo "❌ BUILD FAILED for $project_name"
        echo "═══════════════════════════════════════════════════════════════"
        echo "BUILD ERRORS:"
        echo "═══════════════════════════════════════════════════════════════"
        
        # Display the build errors that were captured
        if [ -f "logs/build-errors-temp.log" ]; then
            cat logs/build-errors-temp.log
            rm -f logs/build-errors-temp.log
        fi
        
        echo "═══════════════════════════════════════════════════════════════"
        echo ""
        echo "🛑 STOPPING EXECUTION DUE TO BUILD ERRORS"
        echo "📝 Please fix the build errors above and try again."
        echo ""
        exit 1
    else
        echo "✅ $project_name build successful"
    fi
}

# Function to build .NET project with error checking
build_dotnet_project() {
    local project_path=$1
    local project_name=$2
    
    echo "🔨 Building $project_name..."
    cd "$project_path"
    
    # Capture build output and errors
    dotnet build --no-restore > ../logs/build-errors-temp.log 2>&1
    local build_result=$?
    
    cd ..
    
    check_build_result "$project_name" $build_result
}

# Function to install npm dependencies and check for errors
install_npm_dependencies() {
    local project_path=$1
    local project_name=$2
    
    echo "📦 Installing $project_name dependencies..."
    cd "$project_path"
    
    # Check if node_modules exists and package.json was modified
    if [ ! -d "node_modules" ] || [ "package.json" -nt "node_modules" ]; then
        npm install > ../logs/build-errors-temp.log 2>&1
        local npm_result=$?
        
        cd ..
        
        if [ $npm_result -ne 0 ]; then
            echo ""
            echo "❌ NPM INSTALL FAILED for $project_name"
            echo "═══════════════════════════════════════════════════════════════"
            echo "NPM ERRORS:"
            echo "═══════════════════════════════════════════════════════════════"
            
            if [ -f "logs/build-errors-temp.log" ]; then
                cat logs/build-errors-temp.log
                rm -f logs/build-errors-temp.log
            fi
            
            echo "═══════════════════════════════════════════════════════════════"
            echo ""
            echo "🛑 STOPPING EXECUTION DUE TO NPM INSTALL ERRORS"
            echo "📝 Please fix the npm install errors above and try again."
            echo ""
            exit 1
        else
            echo "✅ $project_name npm install successful"
        fi
    else
        echo "✅ $project_name dependencies already up to date"
        cd ..
    fi
}

# Clean up old logs first
clean_logs

# Clean up any existing processes
echo "🧹 Cleaning up existing processes..."
kill_port $CLIENT_PORT  # React client port
kill_port $API_PORT     # API port
kill_port $MCP_PORT     # MCP port

echo ""
echo "🔨 Building projects..."
echo "═══════════════════════════════════════════════════════════════"

# Restore NuGet packages first
echo "📦 Restoring NuGet packages..."
dotnet restore > logs/nuget-restore.log 2>&1
if [ $? -ne 0 ]; then
    echo "❌ NuGet restore failed"
    echo "═══════════════════════════════════════════════════════════════"
    cat logs/nuget-restore.log
    echo "═══════════════════════════════════════════════════════════════"
    exit 1
fi
echo "✅ NuGet packages restored"

# Build MCP Server with error checking
build_dotnet_project "SemanticKernelPoc.McpServer" "MCP Server"

# Build API Server with error checking
build_dotnet_project "SemanticKernelPoc.Api" "API Server"

# Install React dependencies with error checking
install_npm_dependencies "SemanticKernelPoc.Web" "React Client"

echo "═══════════════════════════════════════════════════════════════"
echo "✅ All builds completed successfully!"
echo ""

# Start MCP Server
echo "📡 Starting MCP Server..."
cd SemanticKernelPoc.McpServer
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls="https://localhost:$MCP_PORT" > ../logs/mcp-server.log 2>&1 &
MCP_PID=$!
cd ..

# Wait a moment for MCP server to start
sleep 3

# Start API in background
echo "🌐 Starting API Server..."
cd SemanticKernelPoc.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls="https://localhost:$API_PORT" > ../logs/api-server.log 2>&1 &
API_PID=$!
cd ..

# Wait for API to start
sleep 3

# Start React Client in background
echo "⚛️  Starting React Client..."
cd SemanticKernelPoc.Web
npm run dev > ../logs/client.log 2>&1 &
CLIENT_PID=$!
cd ..

# Wait for client to start
sleep 3

# Save PIDs for later cleanup
echo $MCP_PID > logs/mcp-server.pid
echo $API_PID > logs/api-server.pid
echo $CLIENT_PID > logs/client.pid

echo ""
echo "✅ All services started!"
echo "📡 MCP Server PID: $MCP_PID (HTTPS on port $MCP_PORT)"
echo "🌐 API Server PID: $API_PID (HTTPS on port $API_PORT)"
echo "⚛️  React Client PID: $CLIENT_PID (HTTPS on port $CLIENT_PORT)"
echo ""
echo "📊 Service Status:"
echo "   • MCP Server: Running on https://localhost:$MCP_PORT (check logs/mcp-server.log)"
echo "   • API Server: Running on https://localhost:$API_PORT (check logs/api-server.log)"
echo "   • React Client: Running on https://localhost:$CLIENT_PORT (check logs/client.log)"
echo ""
echo "🌐 Application URLs:"
echo "   • React App: https://localhost:$CLIENT_PORT"
echo "   • API Server: https://localhost:$API_PORT"
echo "   • Swagger: https://localhost:$API_PORT/swagger"
echo "   • MCP Server: https://localhost:$MCP_PORT"
echo "   • MCP SSE Endpoint: https://localhost:$MCP_PORT/sse"
echo ""
echo "📝 Logs:"
echo "   • MCP Server: tail -f logs/mcp-server.log"
echo "   • API Server: tail -f logs/api-server.log"
echo "   • React Client: tail -f logs/client.log"
echo "   • NuGet Restore: logs/nuget-restore.log"
echo ""
echo "🛑 To stop all services: ./stop-all.sh"
echo ""
echo "⚠️  Note: You may see browser security warnings for self-signed certificates."
echo "   Accept the certificates for each service to proceed."

# Optional: Open applications in browser (uncomment if desired)
# sleep 2
# open https://localhost:$CLIENT_PORT          # React App
# open https://localhost:$API_PORT/swagger     # API Swagger 