import axios, { AxiosError } from 'axios';
import type { ChatMessage } from '../types/chat';

const API_BASE_URL = 'https://localhost:31338/api';

export class ApiConnectionError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'ApiConnectionError';
    }
}

// Helper function to handle API errors
const handleApiError = (error: unknown): never => {
    if (axios.isAxiosError(error)) {
        const axiosError = error as AxiosError;
        
        // Check if it's a connection error (API not running)
        if (axiosError.code === 'ERR_NETWORK' || 
            axiosError.code === 'ECONNREFUSED' || 
            axiosError.message.includes('Network Error') ||
            axiosError.message.includes('ERR_CONNECTION_REFUSED')) {
            throw new ApiConnectionError(`Unable to connect to the backend API. Please ensure the API server is running on ${API_BASE_URL.replace('/api', '')}`);
        }
        
        // Handle other HTTP errors
        if (axiosError.response) {
            const status = axiosError.response.status;
            if (status === 401) {
                throw new Error('Authentication failed. Please sign in again.');
            } else if (status === 403) {
                throw new Error('Access denied. You do not have permission to perform this action.');
            } else if (status === 500) {
                throw new Error('Internal server error. Please try again later.');
            } else {
                throw new Error(`API request failed with status ${status}: ${axiosError.response.statusText}`);
            }
        }
        
        throw new Error(`Network error: ${axiosError.message}`);
    }
    
    // Handle non-Axios errors
    if (error instanceof Error) {
        throw error;
    }
    
    throw new Error('An unexpected error occurred');
};

export interface UserProfile {
    userId: string;
    userName: string;
    email: string;
    givenName: string;
    surname: string;
    initials: string;
    displayName: string;
}

export const apiService = {
    async getUserProfile(accessToken: string): Promise<UserProfile> {
        try {
            const response = await axios.get(`${API_BASE_URL}/user/profile`, {
                headers: {
                    Authorization: `Bearer ${accessToken}`
                }
            });
            
            return response.data;
        } catch (error) {
            console.error('Failed to get user profile:', error);
            return handleApiError(error);
        }
    },

    async sendMessage(message: ChatMessage, accessToken: string): Promise<ChatMessage> {
        try {
            const response = await axios.post(`${API_BASE_URL}/chat/send`, message, {
                headers: {
                    Authorization: `Bearer ${accessToken}`,
                    'Content-Type': 'application/json'
                }
            });
            
            return response.data;
        } catch (error) {
            console.error('Failed to send message:', error);
            return handleApiError(error);
        }
    },

    async testProtectedEndpoint(accessToken: string) {
        try {
            const response = await axios.get(`${API_BASE_URL}/test/protected`, {
                headers: {
                    Authorization: `Bearer ${accessToken}`
                }
            });
            
            return response.data;
        } catch (error) {
            console.error('Failed to test protected endpoint:', error);
            return handleApiError(error);
        }
    }
}; 