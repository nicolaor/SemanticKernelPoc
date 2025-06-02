# Check Status of SemanticKernelPoc Services (PowerShell)
[CmdletBinding()]
param()

Write-Host "📊 SemanticKernelPoc Service Status" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

# Import get-ports functionality
. .\get-ports.ps1

# Get actual configured ports
$CLIENT_PORT = Get-ClientPort
$API_PORT = Get-ApiPort
$MCP_PORT = Get-McpPort

# Function to check if a port is in use
function Test-Port {
    param([int]$Port)
    
    try {
        $connection = New-Object System.Net.Sockets.TcpClient
        $connection.Connect("127.0.0.1", $Port)
        $connection.Close()
        return $true
    } catch {
        return $false
    }
}

# Function to check if PID is running
function Test-ProcessById {
    param([string]$PidFile)
    
    if (Test-Path $PidFile) {
        try {
            $pid = Get-Content $PidFile -Raw
            $pid = $pid.Trim()
            
            if ($pid -match '^\d+$') {
                $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                if ($process) {
                    Write-Host "✅ Running (PID: $pid)" -ForegroundColor Green
                    return $true
                } else {
                    Write-Host "❌ Not running (stale PID file)" -ForegroundColor Red
                    return $false
                }
            } else {
                Write-Host "❌ Invalid PID in file" -ForegroundColor Red
                return $false
            }
        } catch {
            Write-Host "❌ Error reading PID file" -ForegroundColor Red
            return $false
        }
    } else {
        Write-Host "❌ Not running (no PID file)" -ForegroundColor Red
        return $false
    }
}

# Check MCP Server
Write-Host -NoNewline "📡 MCP Server: "
if (Test-Path "logs\mcp-server.pid") {
    Test-ProcessById "logs\mcp-server.pid" | Out-Null
} else {
    Write-Host "❌ Not running" -ForegroundColor Red
}

# Check API Server
Write-Host -NoNewline "🌐 API Server: "
if (Test-Path "logs\api-server.pid") {
    Test-ProcessById "logs\api-server.pid" | Out-Null
} else {
    Write-Host "❌ Not running" -ForegroundColor Red
}

# Check React Client
Write-Host -NoNewline "⚛️  React Client: "
if (Test-Path "logs\client.pid") {
    Test-ProcessById "logs\client.pid" | Out-Null
} else {
    Write-Host "❌ Not running" -ForegroundColor Red
}

# Check ports
Write-Host ""
Write-Host "🔌 Port Status:" -ForegroundColor Yellow

if (Test-Port $MCP_PORT) {
    Write-Host "   • Port $MCP_PORT (MCP Server): ✅ In use" -ForegroundColor Green
} else {
    Write-Host "   • Port $MCP_PORT (MCP Server): ❌ Available" -ForegroundColor Red
}

if (Test-Port $CLIENT_PORT) {
    Write-Host "   • Port $CLIENT_PORT (React): ✅ In use" -ForegroundColor Green
} else {
    Write-Host "   • Port $CLIENT_PORT (React): ❌ Available" -ForegroundColor Red
}

if (Test-Port $API_PORT) {
    Write-Host "   • Port $API_PORT (API HTTPS): ✅ In use" -ForegroundColor Green
} else {
    Write-Host "   • Port $API_PORT (API HTTPS): ❌ Available" -ForegroundColor Red
}

# Show recent log entries if services are running
Write-Host ""
Write-Host "📝 Recent Logs:" -ForegroundColor Yellow

if (Test-Path "logs\mcp-server.log") {
    Write-Host "   📡 MCP Server (last 3 lines):" -ForegroundColor White
    try {
        $mcpLogs = Get-Content "logs\mcp-server.log" -Tail 3 -ErrorAction SilentlyContinue
        if ($mcpLogs) {
            $mcpLogs | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
        } else {
            Write-Host "      (no recent logs)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "      (error reading logs)" -ForegroundColor Gray
    }
}

if (Test-Path "logs\api-server.log") {
    Write-Host "   🌐 API Server (last 3 lines):" -ForegroundColor White
    try {
        $apiLogs = Get-Content "logs\api-server.log" -Tail 3 -ErrorAction SilentlyContinue
        if ($apiLogs) {
            $apiLogs | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
        } else {
            Write-Host "      (no recent logs)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "      (error reading logs)" -ForegroundColor Gray
    }
}

if (Test-Path "logs\client.log") {
    Write-Host "   ⚛️  React Client (last 3 lines):" -ForegroundColor White
    try {
        $clientLogs = Get-Content "logs\client.log" -Tail 3 -ErrorAction SilentlyContinue
        if ($clientLogs) {
            $clientLogs | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
        } else {
            Write-Host "      (no recent logs)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "      (error reading logs)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🔧 Commands:" -ForegroundColor Cyan
Write-Host "   • Start all: .\start-all.ps1" -ForegroundColor White
Write-Host "   • Stop all: .\stop-all.ps1" -ForegroundColor White
Write-Host "   • View MCP logs: Get-Content logs\mcp-server.log -Wait" -ForegroundColor White
Write-Host "   • View API logs: Get-Content logs\api-server.log -Wait" -ForegroundColor White
Write-Host "   • View Client logs: Get-Content logs\client.log -Wait" -ForegroundColor White

Write-Host ""
Write-Host "🌐 Application URLs:" -ForegroundColor Cyan
Write-Host "   • React App: https://localhost:$CLIENT_PORT" -ForegroundColor White
Write-Host "   • API Swagger: https://localhost:$API_PORT/swagger" -ForegroundColor White
Write-Host "   • MCP Server: https://localhost:$MCP_PORT" -ForegroundColor White
Write-Host "   • MCP SSE Endpoint: https://localhost:$MCP_PORT/sse" -ForegroundColor White
Write-Host "" 