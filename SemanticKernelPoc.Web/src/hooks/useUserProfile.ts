import { useState, useEffect } from "react";
import { useMsal } from "@azure/msal-react";
import { apiService, type UserProfile, ApiConnectionError } from "../services/apiService";
import { loginRequest } from "../config/authConfig";

export const useUserProfile = () => {
  const { instance, accounts } = useMsal();
  const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isApiConnectionError, setIsApiConnectionError] = useState(false);

  useEffect(() => {
    const fetchUserProfile = async () => {
      if (accounts.length === 0) {
        setUserProfile(null);
        return;
      }

      setLoading(true);
      setError(null);
      setIsApiConnectionError(false);

      try {
        // Get access token
        const response = await instance.acquireTokenSilent({
          ...loginRequest,
          account: accounts[0],
        });

        if (response.accessToken) {
          const profile = await apiService.getUserProfile(response.accessToken);
          setUserProfile(profile);
        }
      } catch (err) {
        console.error("Failed to fetch user profile:", err);

        // Check if it's an API connection error
        if (err instanceof ApiConnectionError) {
          setError(err.message);
          setIsApiConnectionError(true);
          return; // Don't try popup for connection errors
        }

        setError("Failed to load user profile");

        // Try to get token with popup if silent fails (for auth errors only)
        try {
          const response = await instance.acquireTokenPopup(loginRequest);
          if (response.accessToken) {
            const profile = await apiService.getUserProfile(response.accessToken);
            setUserProfile(profile);
            setError(null);
            setIsApiConnectionError(false);
          }
        } catch (popupErr) {
          console.error("Failed to acquire token via popup:", popupErr);
          if (popupErr instanceof ApiConnectionError) {
            setError(popupErr.message);
            setIsApiConnectionError(true);
          }
        }
      } finally {
        setLoading(false);
      }
    };

    fetchUserProfile();
  }, [instance, accounts]);

  return { userProfile, loading, error, isApiConnectionError };
};
