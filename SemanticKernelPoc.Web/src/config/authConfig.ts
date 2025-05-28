import type { Configuration, PopupRequest } from "@azure/msal-browser";

// Azure AD configuration
export const msalConfig: Configuration = {
  auth: {
    clientId: "358e70f4-5c15-4c84-9e4c-2e2a7773c735", // Your client ID
    authority: "https://login.microsoftonline.com/29aa64cf-e04e-4a9d-94e7-5fca415f8ed8", // Your tenant ID
    redirectUri: "https://localhost:31337",
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  },
};

// Add scopes here for ID token to be used at Microsoft identity platform endpoints.
export const loginRequest: PopupRequest = {
  scopes: ["api://358e70f4-5c15-4c84-9e4c-2e2a7773c735/access_as_user", "openid", "profile", "email"],
};
