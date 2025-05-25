export interface ChatMessage {
    id: string;
    content: string;
    userId: string;
    userName: string;
    timestamp: string;
    isAiResponse: boolean;
} 