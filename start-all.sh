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

# Clean up any existing processes
echo "🧹 Cleaning up existing processes..."
kill_port $CLIENT_PORT  # React client port
kill_port $API_PORT     # API port
kill_port $MCP_PORT     # MCP port

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
echo ""
echo "🛑 To stop all services: ./stop-all.sh"
echo ""
echo "⚠️  Note: You may see browser security warnings for self-signed certificates."
echo "   Accept the certificates for each service to proceed."

# Optional: Open applications in browser (uncomment if desired)
# sleep 2
# open https://localhost:$CLIENT_PORT          # React App
# open https://localhost:$API_PORT/swagger     # API Swagger 