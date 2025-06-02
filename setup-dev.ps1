# 🛠️ SemanticKernelPoc Development Setup Script (PowerShell)
# This script automates the development environment setup

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "🚀 SemanticKernelPoc Development Setup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Check prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

# Check .NET
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8.0 SDK is required. Please install from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

# Check Node.js
try {
    $nodeVersion = node --version
    Write-Host "✅ Node.js found: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Node.js 18+ is required. Please install from: https://nodejs.org/" -ForegroundColor Red
    exit 1
}

# Check npm
try {
    $npmVersion = npm --version
    Write-Host "✅ npm found: $npmVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ npm is required. Please install Node.js which includes npm." -ForegroundColor Red
    exit 1
}

# Check OpenSSL (Windows might have it in different locations)
$opensslFound = $false
$opensslPaths = @("openssl", "C:\Program Files\Git\usr\bin\openssl.exe", "C:\Program Files\OpenSSL-Win64\bin\openssl.exe")

foreach ($path in $opensslPaths) {
    try {
        $null = & $path version 2>&1
        $opensslFound = $true
        Write-Host "✅ OpenSSL found at: $path" -ForegroundColor Green
        break
    } catch {
        continue
    }
}

if (-not $opensslFound) {
    Write-Host "❌ OpenSSL is required for certificate generation." -ForegroundColor Red
    Write-Host "   Install Git for Windows (includes OpenSSL) or download from: https://slproweb.com/products/Win32OpenSSL.html" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Prerequisites check passed" -ForegroundColor Green

# Install dependencies
Write-Host ""
Write-Host "📦 Installing dependencies..." -ForegroundColor Yellow

Write-Host "Installing .NET dependencies..." -ForegroundColor White
dotnet restore

Write-Host "Installing Node.js dependencies..." -ForegroundColor White
Set-Location SemanticKernelPoc.Web
npm install
Set-Location ..

Write-Host "✅ Dependencies installed" -ForegroundColor Green

# Setup configuration files
Write-Host ""
Write-Host "⚙️ Setting up configuration files..." -ForegroundColor Yellow

$API_CONFIG = "SemanticKernelPoc.Api\appsettings.Development.json"
$API_TEMPLATE = "SemanticKernelPoc.Api\appsettings.Development.template.json"
$REACT_CONFIG = "SemanticKernelPoc.Web\src\config\config.local.json"
$REACT_TEMPLATE = "SemanticKernelPoc.Web\src\config\config.example.json"

# Setup API configuration
if (-not (Test-Path $API_CONFIG)) {
    if (Test-Path $API_TEMPLATE) {
        Write-Host "📄 Copying API configuration template..." -ForegroundColor White
        Copy-Item $API_TEMPLATE $API_CONFIG
        Write-Host "✅ Created $API_CONFIG from template" -ForegroundColor Green
    } else {
        Write-Host "📄 Creating API configuration from scratch..." -ForegroundColor White
        $apiConfigContent = @'
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE"
  },
  "SemanticKernel": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
      "DeploymentName": "gpt-4"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
'@
        $apiConfigContent | Out-File -FilePath $API_CONFIG -Encoding utf8
        Write-Host "✅ Created $API_CONFIG" -ForegroundColor Green
    }
} else {
    Write-Host "✅ API configuration already exists" -ForegroundColor Green
}

# Setup React client configuration
if (-not (Test-Path $REACT_CONFIG)) {
    if (Test-Path $REACT_TEMPLATE) {
        Write-Host "📄 Copying React client configuration template..." -ForegroundColor White
        Copy-Item $REACT_TEMPLATE $REACT_CONFIG
        Write-Host "✅ Created $REACT_CONFIG from template" -ForegroundColor Green
    } else {
        Write-Host "📄 Creating React client configuration from scratch..." -ForegroundColor White
        $reactConfigContent = @'
{
  "azure": {
    "tenantId": "YOUR_TENANT_ID_HERE",
    "clientId": "YOUR_CLIENT_ID_HERE"
  },
  "app": {
    "redirectUri": "https://localhost:31337"
  }
}
'@
        # Ensure directory exists
        $reactConfigDir = Split-Path $REACT_CONFIG -Parent
        if (-not (Test-Path $reactConfigDir)) {
            New-Item -ItemType Directory -Path $reactConfigDir -Force | Out-Null
        }
        $reactConfigContent | Out-File -FilePath $REACT_CONFIG -Encoding utf8
        Write-Host "✅ Created $REACT_CONFIG" -ForegroundColor Green
    }
} else {
    Write-Host "✅ React client configuration already exists" -ForegroundColor Green
}

# Setup certificates
Write-Host ""
Write-Host "🔐 Setting up HTTPS certificates..." -ForegroundColor Yellow

if (-not (Test-Path "certs")) {
    New-Item -ItemType Directory -Path "certs" -Force | Out-Null
}

if (-not (Test-Path "certs\localhost.crt") -or -not (Test-Path "certs\localhost.key")) {
    Write-Host "🔑 Generating self-signed certificate..." -ForegroundColor White
    
    # Find OpenSSL executable
    $openssl = "openssl"
    foreach ($path in $opensslPaths) {
        if (Test-Path $path) {
            $openssl = $path
            break
        }
    }
    
    & $openssl req -x509 -newkey rsa:4096 -keyout certs\localhost.key -out certs\localhost.crt -days 365 -nodes -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
    
    # Create PFX file for .NET
    & $openssl pkcs12 -export -out certs\localhost.pfx -inkey certs\localhost.key -in certs\localhost.crt -passout pass:
    
    Write-Host "✅ Certificate generated" -ForegroundColor Green
} else {
    Write-Host "✅ Certificate already exists" -ForegroundColor Green
}

# Create logs directory
Write-Host ""
Write-Host "📝 Setting up logs directory..." -ForegroundColor Yellow
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" -Force | Out-Null
    Write-Host "✅ Created logs directory" -ForegroundColor Green
} else {
    Write-Host "✅ Logs directory already exists" -ForegroundColor Green
}

# Display next steps
Write-Host ""
Write-Host "🎉 Setup completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Cyan
Write-Host "==============" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. 🔐 Configure Azure AD App Registration:" -ForegroundColor Yellow
Write-Host "   - Go to Azure Portal → Azure Active Directory → App registrations" -ForegroundColor White
Write-Host "   - Create new registration: 'Semantic Kernel PoC'" -ForegroundColor White
Write-Host "   - Redirect URI: https://localhost:31337 (Single-page application)" -ForegroundColor White
Write-Host "   - Configure API permissions (see README for full list):" -ForegroundColor White
Write-Host "     • User.Read, Mail.Read, Mail.Send, Calendars.Read, Calendars.ReadWrite" -ForegroundColor White
Write-Host "     • Files.Read.All, Sites.Read.All, Tasks.ReadWrite" -ForegroundColor White
Write-Host "   - Grant admin consent if required" -ForegroundColor White
Write-Host ""
Write-Host "2. 🤖 Set up Azure OpenAI Service:" -ForegroundColor Yellow
Write-Host "   - Create Azure OpenAI resource in Azure Portal" -ForegroundColor White
Write-Host "   - Deploy GPT-4 model with deployment name 'gpt-4'" -ForegroundColor White
Write-Host "   - Get endpoint and API key from resource overview" -ForegroundColor White
Write-Host ""
Write-Host "3. ⚙️ Update configuration files with your credentials:" -ForegroundColor Yellow
Write-Host "   - Edit: $API_CONFIG" -ForegroundColor White
Write-Host "   - Edit: $REACT_CONFIG" -ForegroundColor White
Write-Host "   - Replace all 'YOUR_*_HERE' placeholders with actual values from Azure" -ForegroundColor White
Write-Host ""
Write-Host "4. 🔒 Trust the SSL certificate (run as Administrator):" -ForegroundColor Yellow
Write-Host "   Import-Certificate -FilePath `"certs\localhost.crt`" -CertStoreLocation Cert:\LocalMachine\Root" -ForegroundColor White
Write-Host ""
Write-Host "5. 🚀 Start the application:" -ForegroundColor Yellow
Write-Host "   .\start-all.ps1" -ForegroundColor White
Write-Host ""

# Security warning about config files
Write-Host "⚠️  SECURITY WARNING:" -ForegroundColor Red
Write-Host "   The configuration files created contain placeholder values." -ForegroundColor Yellow
Write-Host "   Never commit actual API keys or secrets to version control!" -ForegroundColor Yellow
Write-Host "   These files are git-ignored for your security." -ForegroundColor Yellow
Write-Host "" 