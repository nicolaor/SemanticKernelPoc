import { useState, useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { apiService, type UserProfile } from '../services/apiService';
import { loginRequest } from '../config/authConfig';

export const useUserProfile = () => {
    const { instance, accounts } = useMsal();
    const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchUserProfile = async () => {
            if (accounts.length === 0) {
                setUserProfile(null);
                return;
            }

            setLoading(true);
            setError(null);

            try {
                // Get access token
                const response = await instance.acquireTokenSilent({
                    ...loginRequest,
                    account: accounts[0]
                });

                if (response.accessToken) {
                    const profile = await apiService.getUserProfile(response.accessToken);
                    setUserProfile(profile);
                }
            } catch (err) {
                console.error('Failed to fetch user profile:', err);
                setError('Failed to load user profile');
                
                // Try to get token with popup if silent fails
                try {
                    const response = await instance.acquireTokenPopup(loginRequest);
                    if (response.accessToken) {
                        const profile = await apiService.getUserProfile(response.accessToken);
                        setUserProfile(profile);
                        setError(null);
                    }
                } catch (popupErr) {
                    console.error('Failed to acquire token via popup:', popupErr);
                }
            } finally {
                setLoading(false);
            }
        };

        fetchUserProfile();
    }, [instance, accounts]);

    return { userProfile, loading, error };
}; 