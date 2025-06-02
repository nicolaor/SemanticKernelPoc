#!/bin/bash

# Stop All SemanticKernelPoc Services
echo "🛑 Stopping SemanticKernelPoc services..."

# Source port configuration
source ./get-ports.sh

# Get actual configured ports
PORTS=$(get_api_ports)
HTTP_PORT=$(echo $PORTS | cut -d' ' -f1)
HTTPS_PORT=$(echo $PORTS | cut -d' ' -f2)
CLIENT_PORT=$(get_client_port)

# Function to kill process by PID if it exists
kill_pid() {
    if [ -f "$1" ]; then
        PID=$(cat "$1")
        if ps -p $PID > /dev/null 2>&1; then
            echo "🔄 Stopping process $PID..."
            kill $PID 2>/dev/null || true
            sleep 2
            # Force kill if still running
            if ps -p $PID > /dev/null 2>&1; then
                echo "⚡ Force killing process $PID..."
                kill -9 $PID 2>/dev/null || true
            fi
        fi
        rm -f "$1"
    fi
}

# Function to kill process on port
kill_port() {
    if lsof -ti:$1 > /dev/null 2>&1; then
        echo "🔄 Stopping process on port $1..."
        lsof -ti:$1 | xargs kill -9 2>/dev/null || true
        sleep 1
    fi
}

# Stop services by PID files
if [ -d "logs" ]; then
    kill_pid "logs/mcp-server.pid"
    kill_pid "logs/api-server.pid"
    kill_pid "logs/client.pid"
fi

# Also kill by ports as backup
kill_port 8080           # MCP server port
kill_port $CLIENT_PORT  # React client port
kill_port $HTTP_PORT    # API HTTP port
kill_port $HTTPS_PORT   # API HTTPS port

# Kill any remaining processes for our projects
echo "🧹 Cleaning up any remaining processes..."
pkill -f "SemanticKernelPoc.McpServer" 2>/dev/null || true
pkill -f "SemanticKernelPoc.Api" 2>/dev/null || true
pkill -f "vite.*SemanticKernelPoc.Web" 2>/dev/null || true
pkill -f "npm.*dev" 2>/dev/null || true

sleep 2

echo ""
echo "✅ All services stopped!"
echo ""
echo "📝 Log files preserved in logs/ directory"
echo "🚀 To start services again: ./start-all.sh"
echo "" 