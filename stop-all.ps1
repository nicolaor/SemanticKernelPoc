# Stop All SemanticKernelPoc Services (PowerShell)
[CmdletBinding()]
param()

Write-Host "🛑 Stopping SemanticKernelPoc services..." -ForegroundColor Cyan

# Import get-ports functionality
. .\get-ports.ps1

# Get actual configured ports
$CLIENT_PORT = Get-ClientPort
$API_PORT = Get-ApiPort
$MCP_PORT = Get-McpPort

# Function to kill process by PID if it exists
function Stop-PidProcess {
    param([string]$PidFile)
    
    if (Test-Path $PidFile) {
        try {
            $pid = Get-Content $PidFile -Raw
            $pid = $pid.Trim()
            
            if ($pid -match '^\d+$') {
                $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                if ($process) {
                    Write-Host "🔄 Stopping process $pid..." -ForegroundColor Yellow
                    Stop-Process -Id $pid -Force
                    Start-Sleep -Seconds 2
                    
                    # Check if still running and force kill
                    $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                    if ($process) {
                        Write-Host "⚡ Force killing process $pid..." -ForegroundColor Yellow
                        Stop-Process -Id $pid -Force
                    }
                }
            }
        } catch {
            Write-Host "⚠️  Could not stop process from $PidFile" -ForegroundColor Yellow
        }
        
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
    }
}

# Function to kill process on port
function Stop-PortProcess {
    param([int]$Port)
    
    try {
        $processes = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | ForEach-Object { Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue }
        if ($processes) {
            Write-Host "🔄 Stopping process on port $Port..." -ForegroundColor Yellow
            $processes | Stop-Process -Force
            Start-Sleep -Seconds 1
        }
    } catch {
        # Fallback: try netstat approach
        try {
            $netstatResult = netstat -ano | Select-String ":$Port "
            if ($netstatResult) {
                $pids = $netstatResult | ForEach-Object { ($_ -split '\s+')[-1] } | Sort-Object -Unique
                foreach ($pid in $pids) {
                    if ($pid -match '^\d+$') {
                        Write-Host "🔄 Stopping process $pid on port $Port..." -ForegroundColor Yellow
                        try { Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue } catch { }
                    }
                }
            }
        } catch {
            Write-Host "⚠️  Could not stop process on port $Port" -ForegroundColor Yellow
        }
    }
}

# Stop services by PID files
if (Test-Path "logs") {
    Stop-PidProcess "logs\mcp-server.pid"
    Stop-PidProcess "logs\api-server.pid" 
    Stop-PidProcess "logs\client.pid"
}

# Also kill by ports as backup
Stop-PortProcess $MCP_PORT    # MCP server port
Stop-PortProcess $CLIENT_PORT  # React client port
Stop-PortProcess $API_PORT     # API port

# Kill any remaining processes for our projects
Write-Host "🧹 Cleaning up any remaining processes..." -ForegroundColor Yellow

# Kill .NET processes by name
try {
    Get-Process | Where-Object { $_.ProcessName -like "*SemanticKernelPoc*" } | Stop-Process -Force -ErrorAction SilentlyContinue
} catch { }

# Kill Node/npm processes related to our project
try {
    Get-Process | Where-Object { 
        $_.ProcessName -eq "node" -and 
        $_.CommandLine -like "*SemanticKernelPoc.Web*" 
    } | Stop-Process -Force -ErrorAction SilentlyContinue
} catch { }

try {
    Get-Process | Where-Object { 
        $_.ProcessName -eq "npm" -and 
        $_.CommandLine -like "*dev*" 
    } | Stop-Process -Force -ErrorAction SilentlyContinue
} catch { }

# Additional cleanup for common Node processes
try {
    Get-Process | Where-Object { 
        $_.ProcessName -in @("vite", "webpack", "webpack-dev-server") 
    } | Stop-Process -Force -ErrorAction SilentlyContinue
} catch { }

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "✅ All services stopped!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Log files preserved in logs\ directory" -ForegroundColor Gray
Write-Host "🚀 To start services again: .\start-all.ps1" -ForegroundColor White
Write-Host "" 