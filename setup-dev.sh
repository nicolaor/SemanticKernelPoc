#!/bin/bash

# 🛠️ SemanticKernelPoc Development Setup Script
# This script helps set up the development environment

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
MCP_CONFIG="SemanticKernelPoc.McpServer/appsettings.Development.json"

# Check if development config exists
if [ ! -f "$API_CONFIG" ]; then
    if [ -f "$API_TEMPLATE" ]; then
        echo "📄 Copying configuration template..."
        cp "$API_TEMPLATE" "$API_CONFIG"
        echo "✅ Created $API_CONFIG from template"
    else
        echo "⚠️ Template file not found. Creating basic configuration..."
        cat > "$API_CONFIG" << 'EOF'
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "CallbackPath": "/signin-oidc",
    "Scopes": "access_as_user https://graph.microsoft.com/.default",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
    "Audience": "api://YOUR_CLIENT_ID_HERE"
  },
  "SemanticKernel": {
    "DeploymentOrModelId": "gpt-4o-mini",
    "Endpoint": "",
    "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
    "UseAzureOpenAI": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Identity": "Debug"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": [
    "https://localhost:31337"
  ],
  "McpServer": {
    "Url": "https://localhost:31339"
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:31338",
        "Certificate": {
          "Path": "../certs/localhost.crt",
          "KeyPath": "../certs/localhost.key"
        }
      }
    }
  }
}
EOF
    fi
else
    echo "✅ API configuration already exists"
fi

# Create MCP server config if it doesn't exist
if [ ! -f "$MCP_CONFIG" ]; then
    echo "📄 Creating MCP server configuration..."
    cat > "$MCP_CONFIG" << 'EOF'
{
  "AzureAd": {
    "TenantId": "YOUR_TENANT_ID_HERE",
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
EOF
    echo "✅ Created $MCP_CONFIG"
else
    echo "✅ MCP server configuration already exists"
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
    
    # Create PFX file
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
echo "   - Go to Azure Portal > Azure Active Directory > App registrations"
echo "   - Create new registration or use existing one"
echo "   - Configure authentication, permissions, and create client secret"
echo "   - See DEVELOPMENT-SETUP.md for detailed instructions"
echo ""
echo "2. ⚙️ Update configuration files with your credentials:"
echo "   - Edit: $API_CONFIG"
echo "   - Edit: $MCP_CONFIG"
echo "   - Replace all 'YOUR_*_HERE' placeholders with actual values"
echo ""
echo "3. 🔒 Trust the SSL certificate:"

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
    echo "   See DEVELOPMENT-SETUP.md for your operating system"
fi

echo ""
echo "4. 🚀 Start the application:"
echo "   ./start-all.sh"
echo ""
echo "5. 🌐 Access the application:"
echo "   - React App: https://localhost:31337"
echo "   - API Docs: https://localhost:31338/swagger"
echo ""
echo "📖 For detailed setup instructions, see: DEVELOPMENT-SETUP.md"
echo "🆘 For troubleshooting, check the logs in the logs/ directory"
echo ""
echo "Happy coding! 🎯" 