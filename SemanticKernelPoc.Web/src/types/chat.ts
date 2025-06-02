export interface ChatMessage {
  id: string;
  sessionId: string;
  content: string;
  userId: string;
  userName: string;
  isAiResponse: boolean;
  timestamp: string;
  
  // New structured data fields
  cards?: CardData;
  metadata?: ResponseMetadata;
}

export interface CardData {
  type: string; // "tasks", "emails", "calendar", "sharepoint", "capabilities"
  data: any;
  count: number;
  userName?: string;
  timeRange?: string;
}

export interface ResponseMetadata {
  hasCards: boolean;
  originalQuery?: string;
  functionsUsed?: string[];
  processingTime?: number;
}

// Specific card data types for better type safety
export interface TaskCardData {
  id: string;
  title: string;
  content: string;
  status: string;
  priority: string;
  dueDate?: string;
  dueDateFormatted?: string;
  created: string;
  isCompleted: boolean;
  matchReason?: string;
  webLink: string;
  priorityColor: string;
  statusColor: string;
}

export interface EmailCardData {
  id: string;
  subject: string;
  from: string;
  fromEmail: string;
  receivedDate: string;
  receivedDateTime?: string;
  isRead: boolean;
  importance: string;
  preview: string;
  webLink?: string;
  matchReason?: string;
  importanceColor: string;
  readStatusColor: string;
}

export interface CalendarCardData {
  subject: string;
  start: string;
  end: string;
  location: string;
  organizer: string;
  isAllDay: boolean;
  id: string;
  attendees?: AttendeeData[];
}

export interface AttendeeData {
  name: string;
  email: string;
  responseStatus: string;
}

export interface SharePointCardData {
  title: string;
  url: string;
  created: string;
  webTemplate: string;
  description?: string;
}
