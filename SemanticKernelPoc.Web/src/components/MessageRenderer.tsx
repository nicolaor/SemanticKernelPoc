import React from "react";
import CalendarCard from "./CalendarCard";
import NoteCard from "./NoteCard";
import EmailCard from "./EmailCard";
import SharePointCard from "./SharePointCard";
import CapabilitiesCard from "./CapabilitiesCard";
import type { ChatMessage, TaskCardData, EmailCardData, SharePointCardData } from "../types/chat";

interface MessageRendererProps {
  message: ChatMessage;
}

const MessageRenderer: React.FC<MessageRendererProps> = ({ message }) => {
  const { content, isAiResponse, cards } = message;

  // Debug logging to see what we're receiving
  if (isAiResponse && cards) {
    console.log("🔍 MessageRenderer Debug - Cards received:", {
      type: cards.type,
      dataType: typeof cards.data,
      dataIsArray: Array.isArray(cards.data),
      dataLength: Array.isArray(cards.data) ? cards.data.length : 'not array',
      count: cards.count,
      data: cards.data
    });
  }

  // If this is an AI response with structured card data, render cards
  if (isAiResponse && cards) {
    return (
      <div className="space-y-4">
        {/* Render the text content if it exists and is meaningful */}
        {content && content.trim() && content !== "Here are your results:" && (
          <div className="whitespace-pre-line text-sm mb-3" style={{ color: "var(--text-secondary)" }}>
            {content}
          </div>
        )}
        
        {/* Render the appropriate card based on type */}
        {cards.type === "tasks" && (
          <NoteCard notes={Array.isArray(cards.data) ? cards.data as TaskCardData[] : []} />
        )}
        
        {cards.type === "emails" && (
          <EmailCard emails={Array.isArray(cards.data) ? cards.data as EmailCardData[] : []} />
        )}
        
        {cards.type === "calendar" && (
          <CalendarCard data={{
            Type: "calendar_events",
            Count: cards.count || 0,
            UserName: cards.userName || "User",
            TimeRange: cards.timeRange || "recent",
            Events: Array.isArray(cards.data) ? cards.data as any[] : []
          }} />
        )}
        
        {cards.type === "sharepoint" && (
          <SharePointCard sites={Array.isArray(cards.data) ? cards.data as SharePointCardData[] : []} />
        )}
        
        {cards.type === "capabilities" && (
          <CapabilitiesCard data={cards.data} />
        )}
      </div>
    );
  }

  // Legacy fallback: Check if content contains card data (for backward compatibility)
  if (isAiResponse) {
    // Check for capabilities
    if (content.includes("I can assist you with a variety of tasks") || 
        content.includes("Calendar Management") || 
        content.includes("Note-Taking") || 
        content.includes("Email Management") || 
        content.includes("SharePoint and OneDrive")) {
      return (
        <div className="space-y-4">
          <CapabilitiesCard data={{ capabilities: content }} />
        </div>
      );
    }
  }

  // Regular text content
  return <div className="whitespace-pre-line">{content}</div>;
};

export default MessageRenderer;
