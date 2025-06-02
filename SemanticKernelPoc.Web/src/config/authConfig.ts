import type { Configuration, PopupRequest } from "@azure/msal-browser";

// Configuration loading strategy:
// 1. Production: Use environment variables (VITE_SKPOC_TENANT_ID, VITE_SKPOC_CLIENT_ID)
// 2. Development: Use config.local.json (git-ignored)

interface AppConfig {
  azure: {
    tenantId: string;
    clientId: string;
  };
  app: {
    redirectUri: string;
  };
}

// Check environment variables first (for production builds)
const envTenantId = import.meta.env.VITE_SKPOC_TENANT_ID;
const envClientId = import.meta.env.VITE_SKPOC_CLIENT_ID;
const envRedirectUri = import.meta.env.VITE_SKPOC_REDIRECT_URI || "https://localhost:31337";

let config: AppConfig;

if (envTenantId && envClientId) {
  // Production/CI: Use environment variables
  config = {
    azure: {
      tenantId: envTenantId,
      clientId: envClientId,
    },
    app: {
      redirectUri: envRedirectUri,
    },
  };
  console.log('✅ Configuration loaded from environment variables (production mode)');
} else {
  // Development: Load from local config file
  try {
    // Import local config - will throw error if file doesn't exist
    const localConfig = (await import('./config.local.json')).default;
    config = localConfig;
    console.log('✅ Configuration loaded from config.local.json (development mode)');
  } catch (error) {
    console.error('❌ Configuration not found!');
    console.error('📝 For DEVELOPMENT: Copy config.example.json to config.local.json and configure your values');
    console.error('🚀 For PRODUCTION: Set environment variables VITE_SKPOC_TENANT_ID and VITE_SKPOC_CLIENT_ID');
    
    // Provide helpful deployment instructions
    console.error('');
    console.error('🔧 DEPLOYMENT GUIDE:');
    console.error('  Development: cp src/config/config.example.json src/config/config.local.json');
    console.error('  Production:  Set VITE_SKPOC_TENANT_ID and VITE_SKPOC_CLIENT_ID in your CI/CD pipeline');
    
    throw new Error('Configuration missing: Set environment variables or create config.local.json');
  }
}

// Azure AD configuration
export const msalConfig: Configuration = {
  auth: {
    clientId: config.azure.clientId,
    authority: `https://login.microsoftonline.com/${config.azure.tenantId}`,
    redirectUri: config.app.redirectUri,
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  },
};

// Add scopes here for ID token to be used at Microsoft identity platform endpoints.
export const loginRequest: PopupRequest = {
  scopes: [`api://${config.azure.clientId}/access_as_user`, "openid", "profile", "email"],
};
