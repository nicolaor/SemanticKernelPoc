#!/bin/bash

# Get Ports from Configuration
# This script shows the actual port configuration used by start-all.sh

# Define the actual ports used by the application
CLIENT_PORT=31337
API_PORT=31338
MCP_PORT=31339

# Function to get React client port
get_client_port() {
    echo "$CLIENT_PORT"
}

# Function to get API port
get_api_port() {
    echo "$API_PORT"
}

# Function to get MCP server port
get_mcp_port() {
    echo "$MCP_PORT"
}

# Function to get redirect URIs for Entra ID configuration
get_redirect_uris() {
    echo "Redirect URIs for Entra ID app registration:"
    echo "  • https://localhost:$CLIENT_PORT (React Client - for SPA redirect)"
    echo "  • https://localhost:$API_PORT/signin-oidc (API Server - for OIDC callback)"
    echo ""
    echo "Application URLs:"
    echo "  • React Client: https://localhost:$CLIENT_PORT"
    echo "  • API Server: https://localhost:$API_PORT"
    echo "  • Swagger: https://localhost:$API_PORT/swagger"
    echo "  • MCP Server: https://localhost:$MCP_PORT"
}

# Function to check if ports match Entra ID configuration
check_entra_id_config() {
    echo "🔍 Current Application Configuration:"
    echo "   • React Client Port: $CLIENT_PORT (HTTPS)"
    echo "   • API Server Port: $API_PORT (HTTPS)"
    echo "   • MCP Server Port: $MCP_PORT (HTTPS)"
    echo ""
    echo "⚠️  Ensure your Entra ID app registration includes these redirect URIs:"
    echo "   • https://localhost:$CLIENT_PORT (for React SPA)"
    echo "   • https://localhost:$API_PORT/signin-oidc (for API OIDC callback)"
    echo ""
    echo "📝 To update Entra ID app registration:"
    echo "   1. Go to Azure Portal > Entra ID > App registrations"
    echo "   2. Select your app"
    echo "   3. Go to Authentication"
    echo "   4. Add the redirect URIs above"
    echo "   5. Save changes"
    echo ""
    echo "🔧 Application Type Configuration:"
    echo "   • Platform: Single-page application (SPA) for React client"
    echo "   • Platform: Web for API server (if using OIDC flows)"
}

# If script is called directly, show configuration info
if [ "${BASH_SOURCE[0]}" == "${0}" ]; then
    echo "🔧 SemanticKernelPoc Port Configuration"
    echo "======================================"
    echo ""
    check_entra_id_config
fi 