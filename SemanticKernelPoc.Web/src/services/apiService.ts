import axios from 'axios';
import type { ChatMessage } from '../types/chat';

const API_BASE_URL = 'http://localhost:5040/api';

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
        const response = await axios.get(`${API_BASE_URL}/user/profile`, {
            headers: {
                Authorization: `Bearer ${accessToken}`
            }
        });
        return response.data;
    },

    async sendMessage(message: ChatMessage, accessToken: string): Promise<ChatMessage> {
        const response = await axios.post(`${API_BASE_URL}/chat/send`, message, {
            headers: {
                Authorization: `Bearer ${accessToken}`,
                'Content-Type': 'application/json'
            }
        });
        return response.data;
    },

    async testProtectedEndpoint(accessToken: string) {
        const response = await axios.get(`${API_BASE_URL}/test/protected`, {
            headers: {
                Authorization: `Bearer ${accessToken}`
            }
        });
        return response.data;
    }
}; 