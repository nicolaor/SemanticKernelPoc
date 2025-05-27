# HTTPS Configuration for SemanticKernelPoc

This document describes the HTTPS configuration for all services in the SemanticKernelPoc application.

## Overview

All services are now configured to use HTTPS with self-signed certificates:

- **React Client**: https://localhost:31337
- **API Server**: https://localhost:31338  
- **MCP Server**: https://localhost:31339

## Certificate Setup

### Self-Signed Certificates

The application uses self-signed SSL certificates located in the `certs/` directory:

- `localhost.crt` - Certificate file
- `localhost.key` - Private key file
- `localhost.pfx` - PKCS#12 format (for .NET applications)

### Certificate Generation

Certificates are automatically created with the following command:

```bash
cd certs
openssl req -x509 -newkey rsa:4096 -keyout localhost.key -out localhost.crt -days 365 -nodes \
  -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

# Create PFX format for .NET
openssl pkcs12 -export -out localhost.pfx -inkey localhost.key -in localhost.crt -passout pass:
```

## Service Configuration

### API Server (SemanticKernelPoc.Api)

**appsettings.json**:
```json
{
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
  },
  "McpServer": {
    "Url": "https://localhost:31339"
  },
  "AllowedOrigins": [
    "https://localhost:31337"
  ]
}
```

### MCP Server (SemanticKernelPoc.McpServer)

**appsettings.json**:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:31339",
        "Certificate": {
          "Path": "../certs/localhost.crt",
          "KeyPath": "../certs/localhost.key"
        }
      }
    }
  }
}
```

### React Client (SemanticKernelPoc.Web)

**vite.config.ts**:
```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  plugins: [react()],
  server: {
    port: 31337,
    host: true,
    https: {
      key: readFileSync(resolve(__dirname, '../certs/localhost.key')),
      cert: readFileSync(resolve(__dirname, '../certs/localhost.crt'))
    }
  }
})
```

**src/services/apiService.ts**:
```typescript
const API_BASE_URL = 'https://localhost:31338/api';
```

## Starting Services

Use the startup script to start all services with HTTPS:

```bash
./start-all.sh
```

This will start:
- MCP Server on https://localhost:31339
- API Server on https://localhost:31338
- React Client on https://localhost:31337

## Browser Security Warnings

Since we're using self-signed certificates, browsers will show security warnings. You need to:

1. **Accept the certificate for each service**:
   - Visit https://localhost:31337 (React app) - accept certificate
   - Visit https://localhost:31338 (API server) - accept certificate  
   - Visit https://localhost:31339 (MCP server) - accept certificate

2. **Chrome/Edge**: Click "Advanced" → "Proceed to localhost (unsafe)"
3. **Firefox**: Click "Advanced" → "Accept the Risk and Continue"
4. **Safari**: Click "Show Details" → "visit this website"

## Service URLs

After starting services and accepting certificates:

- **React Application**: https://localhost:31337
- **API Swagger Documentation**: https://localhost:31338/swagger
- **MCP Server SSE Endpoint**: https://localhost:31339/sse

## Troubleshooting

### Certificate Issues

If you encounter certificate-related errors:

1. **Regenerate certificates**:
   ```bash
   rm -rf certs/*
   cd certs
   openssl req -x509 -newkey rsa:4096 -keyout localhost.key -out localhost.crt -days 365 -nodes \
     -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost" \
     -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
   openssl pkcs12 -export -out localhost.pfx -inkey localhost.key -in localhost.crt -passout pass:
   ```

2. **Clear browser cache** and restart services

3. **Check certificate paths** in configuration files

### Connection Issues

If services can't connect to each other:

1. **Verify all services are running on HTTPS**:
   ```bash
   curl -k https://localhost:31337  # Should return 200
   curl -k https://localhost:31338  # Should return 404 (no route)
   curl -k https://localhost:31339  # Should return 406 (method not allowed)
   ```

2. **Check logs**:
   ```bash
   tail -f logs/mcp-server.log
   tail -f logs/api-server.log
   tail -f logs/client.log
   ```

3. **Verify configuration** matches certificate paths

## Security Notes

⚠️ **Important**: These are self-signed certificates for development only. 

For production deployment:
- Use certificates from a trusted Certificate Authority (CA)
- Configure proper domain names instead of localhost
- Use environment-specific configuration files
- Consider using reverse proxy (nginx/Apache) for SSL termination

## File Structure

```
SemanticKernelPoc/
├── certs/
│   ├── localhost.crt     # SSL certificate
│   ├── localhost.key     # Private key
│   └── localhost.pfx     # PKCS#12 format
├── SemanticKernelPoc.Api/
│   └── appsettings.json  # HTTPS configuration
├── SemanticKernelPoc.McpServer/
│   └── appsettings.json  # HTTPS configuration
├── SemanticKernelPoc.Web/
│   ├── vite.config.ts    # HTTPS configuration
│   └── src/services/apiService.ts  # HTTPS API URLs
└── start-all.sh          # HTTPS startup script
``` 