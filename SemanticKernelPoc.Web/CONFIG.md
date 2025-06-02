# 🔧 Configuration Setup

## 🔒 Secure Local Configuration

This project uses local configuration files that are **automatically excluded from git** to keep your sensitive Azure AD credentials secure.

### ⚡ Quick Setup

1. **Copy the example configuration:**
   ```bash
   cp src/config/config.example.json src/config/config.local.json
   ```

2. **Edit your local config with your Azure AD values:**
   ```json
   {
     "azure": {
       "tenantId": "your-actual-tenant-id",
       "clientId": "your-actual-client-id"
     },
     "app": {
       "redirectUri": "https://localhost:31337"
     }
   }
   ```

3. **Save and run the app** - your values will be automatically loaded!

### 🛡️ Security Features

- ✅ **Git-ignored**: `config.local.json` is never committed
- ✅ **Fallback safe**: App works with defaults if local config missing
- ✅ **Example provided**: Template file shows required structure
- ✅ **No environment variables**: Simple file-based approach

### 📁 Files

- `config.local.json` - **Your actual config** (git-ignored, contains real values)
- `config.example.json` - **Template file** (committed, contains placeholders)
- `authConfig.ts` - **Loads configuration** (automatically uses local config)

### 🚨 Important Notes

- **Never commit `config.local.json`** - It contains your real credentials
- **Always use the example file** as a template for new environments
- **The app will warn** if local config is missing but still work with defaults

### 🔍 Troubleshooting

If you see a warning about missing local config:
1. Check that `config.local.json` exists in `src/config/`
2. Verify the JSON structure matches the example
3. Ensure your tenant ID and client ID are correct 