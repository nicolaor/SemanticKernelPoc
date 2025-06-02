#!/bin/bash

# 🛠️ SemanticKernelPoc Development Setup Script
# This script automates the development environment setup

set -e

echo "🚀 SemanticKernelPoc Development Setup"
echo "======================================"

# Check prerequisites
echo "📋 Checking prerequisites..."

# Check .NET
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET 8.0 SDK is required. Please install from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Check Node.js
if ! command -v node &> /dev/null; then
    echo "❌ Node.js 18+ is required. Please install from: https://nodejs.org/"
    exit 1
fi

# Check npm
if ! command -v npm &> /dev/null; then
    echo "❌ npm is required. Please install Node.js which includes npm."
    exit 1
fi

# Check OpenSSL
if ! command -v openssl &> /dev/null; then
    echo "❌ OpenSSL is required for certificate generation."
    exit 1
fi

echo "✅ Prerequisites check passed"

# Install dependencies
echo ""
echo "📦 Installing dependencies..."

echo "Installing .NET dependencies..."
dotnet restore

echo "Installing Node.js dependencies..."
cd SemanticKernelPoc.Web
npm install
cd ..

echo "✅ Dependencies installed"

# Setup configuration files
echo ""
echo "⚙️ Setting up configuration files..."

API_CONFIG="SemanticKernelPoc.Api/appsettings.Development.json"
API_TEMPLATE="SemanticKernelPoc.Api/appsettings.Development.template.json"
REACT_CONFIG="SemanticKernelPoc.Web/src/config/config.local.json"
REACT_TEMPLATE="SemanticKernelPoc.Web/src/config/config.example.json"

# Setup API configuration
if [ ! -f "$API_CONFIG" ]; then
    if [ -f "$API_TEMPLATE" ]; then
        echo "📄 Copying API configuration template..."
        cp "$API_TEMPLATE" "$API_CONFIG"
        echo "✅ Created $API_CONFIG from template"
    else
        echo "📄 Creating API configuration from scratch..."
        cat > "$API_CONFIG" << 'EOF'
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
EOF
        echo "✅ Created $API_CONFIG"
    fi
else
    echo "✅ API configuration already exists"
fi

# Setup React client configuration
if [ ! -f "$REACT_CONFIG" ]; then
    if [ -f "$REACT_TEMPLATE" ]; then
        echo "📄 Copying React client configuration template..."
        cp "$REACT_TEMPLATE" "$REACT_CONFIG"
        echo "✅ Created $REACT_CONFIG from template"
    else
        echo "📄 Creating React client configuration from scratch..."
        cat > "$REACT_CONFIG" << 'EOF'
{
  "azure": {
    "tenantId": "YOUR_TENANT_ID_HERE",
    "clientId": "YOUR_CLIENT_ID_HERE"
  },
  "app": {
    "redirectUri": "https://localhost:31337"
  }
}
EOF
        echo "✅ Created $REACT_CONFIG"
    fi
else
    echo "✅ React client configuration already exists"
fi

# Setup certificates
echo ""
echo "🔐 Setting up HTTPS certificates..."

if [ ! -d "certs" ]; then
    mkdir -p certs
fi

if [ ! -f "certs/localhost.crt" ] || [ ! -f "certs/localhost.key" ]; then
    echo "🔑 Generating self-signed certificate..."
    openssl req -x509 -newkey rsa:4096 -keyout certs/localhost.key -out certs/localhost.crt -days 365 -nodes \
        -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
    
    # Create PFX file for .NET
    openssl pkcs12 -export -out certs/localhost.pfx -inkey certs/localhost.key -in certs/localhost.crt -passout pass:
    
    echo "✅ Certificate generated"
else
    echo "✅ Certificate already exists"
fi

# Create logs directory
echo ""
echo "📝 Setting up logs directory..."
if [ ! -d "logs" ]; then
    mkdir -p logs
    echo "✅ Created logs directory"
else
    echo "✅ Logs directory already exists"
fi

# Display next steps
echo ""
echo "🎉 Setup completed successfully!"
echo ""
echo "📋 Next Steps:"
echo "=============="
echo ""
echo "1. 🔐 Configure Azure AD App Registration:"
echo "   - Go to Azure Portal → Azure Active Directory → App registrations"
echo "   - Create new registration: 'Semantic Kernel PoC'"
echo "   - Redirect URI: https://localhost:31337 (Single-page application)"
echo "   - Configure API permissions (see README for full list):"
echo "     • User.Read, Mail.Read, Mail.Send, Calendars.Read, Calendars.ReadWrite"
echo "     • Files.Read.All, Sites.Read.All, Tasks.ReadWrite"
echo "   - Grant admin consent if required"
echo ""
echo "2. 🤖 Set up Azure OpenAI Service:"
echo "   - Create Azure OpenAI resource in Azure Portal"
echo "   - Deploy GPT-4 model with deployment name 'gpt-4'"
echo "   - Get endpoint and API key from resource overview"
echo ""
echo "3. ⚙️ Update configuration files with your credentials:"
echo "   - Edit: $API_CONFIG"
echo "   - Edit: $REACT_CONFIG"
echo "   - Replace all 'YOUR_*_HERE' placeholders with actual values from Azure"
echo ""
echo "4. 🔒 Trust the SSL certificate:"

# OS-specific certificate trust instructions
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo "   sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/localhost.crt"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "   sudo cp certs/localhost.crt /usr/local/share/ca-certificates/"
    echo "   sudo update-ca-certificates"
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
    echo "   # Run as Administrator in PowerShell:"
    echo "   Import-Certificate -FilePath \"certs\\localhost.crt\" -CertStoreLocation Cert:\\LocalMachine\\Root"
else
    echo "   See README for platform-specific instructions"
fi

echo ""
echo "5. 🚀 Start the application:"
echo "   ./start-all.sh"
echo ""
echo "6. 🌐 Access the application:"
echo "   - React App: https://localhost:31337"
echo "   - API Docs: https://localhost:31338/swagger"
echo "   - MCP Server: http://localhost:3001 (internal)"
echo ""
echo "📖 For detailed setup instructions and troubleshooting, see the README.md"
echo "🔍 Monitor logs in real-time: tail -f logs/api-server.log"
echo ""
echo "⚠️  Important Security Notes:"
echo "   • Configuration files with secrets are git-ignored"
echo "   • Never commit files containing 'YOUR_*_HERE' values"
echo "   • Use the template files for sharing configuration structure"
echo ""
echo "Happy coding! 🎯" 