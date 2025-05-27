#!/bin/bash

# Check Status of SemanticKernelPoc Services
echo "📊 SemanticKernelPoc Service Status"
echo "=================================="

# Source port configuration
source ./get-ports.sh

# Get actual configured ports
PORTS=$(get_api_ports)
HTTP_PORT=$(echo $PORTS | cut -d' ' -f1)
HTTPS_PORT=$(echo $PORTS | cut -d' ' -f2)
CLIENT_PORT=$(get_client_port)

# Function to check if a port is in use
check_port() {
    lsof -ti:$1 > /dev/null 2>&1
}

# Function to check if PID is running
check_pid() {
    if [ -f "$1" ]; then
        PID=$(cat "$1")
        if ps -p $PID > /dev/null 2>&1; then
            echo "✅ Running (PID: $PID)"
            return 0
        else
            echo "❌ Not running (stale PID file)"
            return 1
        fi
    else
        echo "❌ Not running (no PID file)"
        return 1
    fi
}

# Check MCP Server
echo -n "📡 MCP Server: "
if [ -f "logs/mcp-server.pid" ]; then
    check_pid "logs/mcp-server.pid"
else
    echo "❌ Not running"
fi

# Check API Server
echo -n "🌐 API Server: "
if [ -f "logs/api-server.pid" ]; then
    check_pid "logs/api-server.pid"
else
    echo "❌ Not running"
fi

# Check React Client
echo -n "⚛️  React Client: "
if [ -f "logs/client.pid" ]; then
    check_pid "logs/client.pid"
else
    echo "❌ Not running"
fi

# Check ports
echo ""
echo "🔌 Port Status:"
if check_port 8080; then
    echo "   • Port 8080 (MCP Server): ✅ In use"
else
    echo "   • Port 8080 (MCP Server): ❌ Available"
fi

if check_port $CLIENT_PORT; then
    echo "   • Port $CLIENT_PORT (React): ✅ In use"
else
    echo "   • Port $CLIENT_PORT (React): ❌ Available"
fi

if check_port $HTTP_PORT; then
    echo "   • Port $HTTP_PORT (API HTTP): ✅ In use"
else
    echo "   • Port $HTTP_PORT (API HTTP): ❌ Available"
fi

if check_port $HTTPS_PORT; then
    echo "   • Port $HTTPS_PORT (API HTTPS): ✅ In use"
else
    echo "   • Port $HTTPS_PORT (API HTTPS): ❌ Available"
fi

# Show recent log entries if services are running
echo ""
echo "📝 Recent Logs:"
if [ -f "logs/mcp-server.log" ]; then
    echo "   📡 MCP Server (last 3 lines):"
    tail -3 logs/mcp-server.log | sed 's/^/      /'
fi

if [ -f "logs/api-server.log" ]; then
    echo "   🌐 API Server (last 3 lines):"
    tail -3 logs/api-server.log | sed 's/^/      /'
fi

if [ -f "logs/client.log" ]; then
    echo "   ⚛️  React Client (last 3 lines):"
    tail -3 logs/client.log | sed 's/^/      /'
fi

echo ""
echo "🔧 Commands:"
echo "   • Start all: ./start-all.sh"
echo "   • Stop all: ./stop-all.sh"
echo "   • View MCP logs: tail -f logs/mcp-server.log"
echo "   • View API logs: tail -f logs/api-server.log"
echo "   • View Client logs: tail -f logs/client.log"
echo ""
echo "🌐 Application URLs:"
echo "   • React App: http://localhost:$CLIENT_PORT"
echo "   • API Swagger: https://localhost:$HTTPS_PORT/swagger"
echo "   • MCP Server: http://localhost:8080"
echo "   • MCP SSE Endpoint: http://localhost:8080/sse"
echo "" 