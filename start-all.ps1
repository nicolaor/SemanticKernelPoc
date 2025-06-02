# Start All SemanticKernelPoc Services (PowerShell)
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting SemanticKernelPoc services..." -ForegroundColor Cyan

# Define ports
$CLIENT_PORT = 31337
$API_PORT = 31338
$MCP_PORT = 31339

Write-Host "📋 Using HTTPS ports: Client=$CLIENT_PORT, API=$API_PORT, MCP=$MCP_PORT" -ForegroundColor Yellow

# Create logs directory if it doesn't exist
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" -Force | Out-Null
}

# Function to clean log files
function Clean-Logs {
    Write-Host "🧹 Cleaning up old log files..." -ForegroundColor Yellow
    if (Test-Path "logs\*.log") { Remove-Item "logs\*.log" -Force }
    if (Test-Path "logs\*.pid") { Remove-Item "logs\*.pid" -Force }
    Write-Host "✅ Log files cleaned" -ForegroundColor Green
}

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

# Function to kill process on port
function Stop-PortProcess {
    param([int]$Port)
    
    if (Test-Port $Port) {
        Write-Host "⚠️  Port $Port is in use, killing existing process..." -ForegroundColor Yellow
        try {
            $processes = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | ForEach-Object { Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue }
            if ($processes) {
                $processes | Stop-Process -Force
                Start-Sleep -Seconds 2
            }
        } catch {
            # Fallback: try netstat approach
            $netstatResult = netstat -ano | Select-String ":$Port "
            if ($netstatResult) {
                $pids = $netstatResult | ForEach-Object { ($_ -split '\s+')[-1] } | Sort-Object -Unique
                foreach ($pid in $pids) {
                    if ($pid -match '^\d+$') {
                        try { Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue } catch { }
                    }
                }
            }
        }
    }
}

# Function to check build result and handle errors
function Test-BuildResult {
    param(
        [string]$ProjectName,
        [int]$ExitCode,
        [string]$ErrorLog
    )
    
    if ($ExitCode -ne 0) {
        Write-Host ""
        Write-Host "❌ BUILD FAILED for $ProjectName" -ForegroundColor Red
        Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
        Write-Host "BUILD ERRORS:" -ForegroundColor Red
        Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
        
        if (Test-Path $ErrorLog) {
            Get-Content $ErrorLog | Write-Host -ForegroundColor Red
            Remove-Item $ErrorLog -Force
        }
        
        Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
        Write-Host ""
        Write-Host "🛑 STOPPING EXECUTION DUE TO BUILD ERRORS" -ForegroundColor Red
        Write-Host "📝 Please fix the build errors above and try again." -ForegroundColor Yellow
        Write-Host ""
        exit 1
    } else {
        Write-Host "✅ $ProjectName build successful" -ForegroundColor Green
    }
}

# Function to build .NET project with error checking
function Build-DotNetProject {
    param(
        [string]$ProjectPath,
        [string]$ProjectName
    )
    
    Write-Host "🔨 Building $ProjectName..." -ForegroundColor Yellow
    $currentLocation = Get-Location
    Set-Location $ProjectPath
    
    $errorLog = Join-Path $currentLocation "logs\build-errors-temp.log"
    
    # Capture build output and errors
    $process = Start-Process -FilePath "dotnet" -ArgumentList "build", "--no-restore" -RedirectStandardOutput $errorLog -RedirectStandardError $errorLog -Wait -PassThru -NoNewWindow
    
    Set-Location $currentLocation
    
    Test-BuildResult $ProjectName $process.ExitCode $errorLog
}

# Function to install npm dependencies and check for errors
function Install-NpmDependencies {
    param(
        [string]$ProjectPath,
        [string]$ProjectName
    )
    
    Write-Host "📦 Installing $ProjectName dependencies..." -ForegroundColor Yellow
    $currentLocation = Get-Location
    Set-Location $ProjectPath
    
    # Check if node_modules exists and package.json was modified
    $needsInstall = $false
    if (-not (Test-Path "node_modules")) {
        $needsInstall = $true
    } elseif ((Get-Item "package.json").LastWriteTime -gt (Get-Item "node_modules").LastWriteTime) {
        $needsInstall = $true
    }
    
    if ($needsInstall) {
        $errorLog = Join-Path $currentLocation "logs\build-errors-temp.log"
        $process = Start-Process -FilePath "npm" -ArgumentList "install" -RedirectStandardOutput $errorLog -RedirectStandardError $errorLog -Wait -PassThru -NoNewWindow
        
        Set-Location $currentLocation
        
        if ($process.ExitCode -ne 0) {
            Write-Host ""
            Write-Host "❌ NPM INSTALL FAILED for $ProjectName" -ForegroundColor Red
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
            Write-Host "NPM ERRORS:" -ForegroundColor Red
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
            
            if (Test-Path $errorLog) {
                Get-Content $errorLog | Write-Host -ForegroundColor Red
                Remove-Item $errorLog -Force
            }
            
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
            Write-Host ""
            Write-Host "🛑 STOPPING EXECUTION DUE TO NPM INSTALL ERRORS" -ForegroundColor Red
            Write-Host "📝 Please fix the npm install errors above and try again." -ForegroundColor Yellow
            Write-Host ""
            exit 1
        } else {
            Write-Host "✅ $ProjectName npm install successful" -ForegroundColor Green
        }
    } else {
        Write-Host "✅ $ProjectName dependencies already up to date" -ForegroundColor Green
        Set-Location $currentLocation
    }
}

# Clean up old logs first
Clean-Logs

# Clean up any existing processes
Write-Host "🧹 Cleaning up existing processes..." -ForegroundColor Yellow
Stop-PortProcess $CLIENT_PORT  # React client port
Stop-PortProcess $API_PORT     # API port
Stop-PortProcess $MCP_PORT     # MCP port

Write-Host ""
Write-Host "🔨 Building projects..." -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Restore NuGet packages first
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
$process = Start-Process -FilePath "dotnet" -ArgumentList "restore" -RedirectStandardOutput "logs\nuget-restore.log" -RedirectStandardError "logs\nuget-restore.log" -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) {
    Write-Host "❌ NuGet restore failed" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Get-Content "logs\nuget-restore.log" | Write-Host -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
    exit 1
}
Write-Host "✅ NuGet packages restored" -ForegroundColor Green

# Build MCP Server with error checking
Build-DotNetProject "SemanticKernelPoc.McpServer" "MCP Server"

# Build API Server with error checking
Build-DotNetProject "SemanticKernelPoc.Api" "API Server"

# Install React dependencies with error checking
Install-NpmDependencies "SemanticKernelPoc.Web" "React Client"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ All builds completed successfully!" -ForegroundColor Green
Write-Host ""

# Start MCP Server
Write-Host "📡 Starting MCP Server..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
$mcpProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--urls=https://localhost:$MCP_PORT" -WorkingDirectory "SemanticKernelPoc.McpServer" -RedirectStandardOutput "logs\mcp-server.log" -RedirectStandardError "logs\mcp-server.log" -PassThru

# Wait a moment for MCP server to start
Start-Sleep -Seconds 3

# Start API in background
Write-Host "🌐 Starting API Server..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--urls=https://localhost:$API_PORT" -WorkingDirectory "SemanticKernelPoc.Api" -RedirectStandardOutput "logs\api-server.log" -RedirectStandardError "logs\api-server.log" -PassThru

# Wait for API to start
Start-Sleep -Seconds 3

# Start React Client in background
Write-Host "⚛️  Starting React Client..." -ForegroundColor Yellow
$clientProcess = Start-Process -FilePath "npm" -ArgumentList "run", "dev" -WorkingDirectory "SemanticKernelPoc.Web" -RedirectStandardOutput "logs\client.log" -RedirectStandardError "logs\client.log" -PassThru

# Wait for client to start
Start-Sleep -Seconds 3

# Save PIDs for later cleanup
$mcpProcess.Id | Out-File "logs\mcp-server.pid" -Encoding ascii
$apiProcess.Id | Out-File "logs\api-server.pid" -Encoding ascii
$clientProcess.Id | Out-File "logs\client.pid" -Encoding ascii

Write-Host ""
Write-Host "✅ All services started!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Application URLs:" -ForegroundColor Cyan
Write-Host "   • React App: https://localhost:$CLIENT_PORT" -ForegroundColor White
Write-Host "   • API Swagger: https://localhost:$API_PORT/swagger" -ForegroundColor White
Write-Host "   • MCP Server: https://localhost:$MCP_PORT" -ForegroundColor White
Write-Host ""
Write-Host "📝 Monitoring:" -ForegroundColor Cyan
Write-Host "   • Check status: .\status.ps1" -ForegroundColor White
Write-Host "   • View logs: Get-Content logs\api-server.log -Wait" -ForegroundColor White
Write-Host "   • Stop all: .\stop-all.ps1" -ForegroundColor White
Write-Host ""
Write-Host "📋 Process IDs saved to logs\*.pid files" -ForegroundColor Gray

# Keep script running to monitor processes
Write-Host "Press Ctrl+C to stop monitoring (services will continue running)" -ForegroundColor Yellow
try {
    while ($true) {
        Start-Sleep -Seconds 5
        # Check if any process has stopped
        if ($mcpProcess.HasExited) {
            Write-Host "⚠️  MCP Server has stopped unexpectedly" -ForegroundColor Red
        }
        if ($apiProcess.HasExited) {
            Write-Host "⚠️  API Server has stopped unexpectedly" -ForegroundColor Red
        }
        if ($clientProcess.HasExited) {
            Write-Host "⚠️  React Client has stopped unexpectedly" -ForegroundColor Red
        }
    }
} catch {
    Write-Host ""
    Write-Host "👋 Monitoring stopped. Services are still running in background." -ForegroundColor Yellow
    Write-Host "   Use .\stop-all.ps1 to stop all services." -ForegroundColor White
} 