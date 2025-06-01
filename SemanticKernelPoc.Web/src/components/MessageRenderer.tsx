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
          <NoteCard notes={cards.data as TaskCardData[]} />
        )}
        
        {cards.type === "emails" && (
          <EmailCard emails={cards.data as EmailCardData[]} />
        )}
        
        {cards.type === "calendar" && (
          <CalendarCard data={{
            Type: "calendar_events",
            Count: cards.count || 0,
            UserName: cards.userName || "User",
            TimeRange: cards.timeRange || "recent",
            Events: cards.data as any[]
          }} />
        )}
        
        {cards.type === "sharepoint" && (
          <SharePointCard sites={cards.data as SharePointCardData[]} />
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

    // Check for legacy card formats (for backward compatibility during transition)
    if (content.startsWith("CALENDAR_CARDS:")) {
      try {
        const jsonData = content.replace("CALENDAR_CARDS:", "");
        const calendarData = JSON.parse(jsonData);
        if (calendarData.Type === "calendar_events" && calendarData.Events) {
          return (
            <div className="space-y-4">
              <CalendarCard data={calendarData} />
            </div>
          );
        }
      } catch (error) {
        console.error("Failed to parse legacy calendar data:", error);
      }
    }

    if (content.startsWith("TASK_CARDS:")) {
      try {
        const jsonData = content.replace("TASK_CARDS:", "").trim();
        const taskData = JSON.parse(jsonData);
        if (Array.isArray(taskData)) {
          return (
            <div className="space-y-4">
              <NoteCard notes={taskData} />
            </div>
          );
        }
      } catch (error) {
        console.error("Failed to parse legacy task data:", error);
      }
    }

    if (content.startsWith("EMAIL_CARDS:")) {
      try {
        const jsonData = content.replace("EMAIL_CARDS:", "");
        const emailData = JSON.parse(jsonData);
        if (Array.isArray(emailData)) {
          return (
            <div className="space-y-4">
              <EmailCard emails={emailData} />
            </div>
          );
        }
      } catch (error) {
        console.error("Failed to parse legacy email data:", error);
      }
    }

    if (content.startsWith("SHAREPOINT_CARDS:")) {
      try {
        const jsonData = content.replace("SHAREPOINT_CARDS:", "").trim();
        const sharePointData = JSON.parse(jsonData);
        if (Array.isArray(sharePointData)) {
          return (
            <div className="space-y-4">
              <SharePointCard sites={sharePointData} />
            </div>
          );
        }
      } catch (error) {
        console.error("Failed to parse legacy SharePoint data:", error);
      }
    }
  }

  // Regular text content
  return <div className="whitespace-pre-line">{content}</div>;
};

export default MessageRenderer;
