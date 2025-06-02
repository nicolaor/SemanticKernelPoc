# Get Ports from Configuration (PowerShell)
# This script shows the actual port configuration used by start-all.ps1

# Define the actual ports used by the application
$CLIENT_PORT = 31337
$API_PORT = 31338
$MCP_PORT = 31339

# Function to get React client port
function Get-ClientPort {
    return $CLIENT_PORT
}

# Function to get API port
function Get-ApiPort {
    return $API_PORT
}

# Function to get MCP server port
function Get-McpPort {
    return $MCP_PORT
}

# Function to get redirect URIs for Entra ID configuration
function Get-RedirectUris {
    Write-Host "Redirect URIs for Entra ID app registration:" -ForegroundColor Yellow
    Write-Host "  • https://localhost:$CLIENT_PORT (React Client - for SPA redirect)" -ForegroundColor White
    Write-Host "  • https://localhost:$API_PORT/signin-oidc (API Server - for OIDC callback)" -ForegroundColor White
    Write-Host ""
    Write-Host "Application URLs:" -ForegroundColor Yellow
    Write-Host "  • React Client: https://localhost:$CLIENT_PORT" -ForegroundColor White
    Write-Host "  • API Server: https://localhost:$API_PORT" -ForegroundColor White
    Write-Host "  • Swagger: https://localhost:$API_PORT/swagger" -ForegroundColor White
    Write-Host "  • MCP Server: https://localhost:$MCP_PORT" -ForegroundColor White
}

# Function to check if ports match Entra ID configuration
function Test-EntraIdConfig {
    Write-Host "🔍 Current Application Configuration:" -ForegroundColor Cyan
    Write-Host "   • React Client Port: $CLIENT_PORT (HTTPS)" -ForegroundColor White
    Write-Host "   • API Server Port: $API_PORT (HTTPS)" -ForegroundColor White
    Write-Host "   • MCP Server Port: $MCP_PORT (HTTPS)" -ForegroundColor White
    Write-Host ""
    Write-Host "⚠️  Ensure your Entra ID app registration includes these redirect URIs:" -ForegroundColor Yellow
    Write-Host "   • https://localhost:$CLIENT_PORT (for React SPA)" -ForegroundColor White
    Write-Host "   • https://localhost:$API_PORT/signin-oidc (for API OIDC callback)" -ForegroundColor White
    Write-Host ""
    Write-Host "📝 To update Entra ID app registration:" -ForegroundColor Yellow
    Write-Host "   1. Go to Azure Portal > Entra ID > App registrations" -ForegroundColor White
    Write-Host "   2. Select your app" -ForegroundColor White
    Write-Host "   3. Go to Authentication" -ForegroundColor White
    Write-Host "   4. Add the redirect URIs above" -ForegroundColor White
    Write-Host "   5. Save changes" -ForegroundColor White
    Write-Host ""
    Write-Host "🔧 Application Type Configuration:" -ForegroundColor Yellow
    Write-Host "   • Platform: Single-page application (SPA) for React client" -ForegroundColor White
    Write-Host "   • Platform: Web for API server (if using OIDC flows)" -ForegroundColor White
}

# If script is called directly, show configuration info
if ($MyInvocation.ScriptName -eq $PSCommandPath) {
    Write-Host "🔧 SemanticKernelPoc Port Configuration" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
    Test-EntraIdConfig
} 