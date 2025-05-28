import React from "react";
import CalendarCard from "./CalendarCard";
import NoteCard from "./NoteCard";
import CapabilitiesCard from "./CapabilitiesCard";

interface MessageRendererProps {
  content: string;
  isAiResponse?: boolean;
}

const MessageRenderer: React.FC<MessageRendererProps> = ({ content, isAiResponse = false }) => {
  // Check if content contains capabilities information and it's an AI response
  if (isAiResponse && (content.includes("I can assist you with a variety of tasks") || content.includes("Calendar Management") || content.includes("Note-Taking") || content.includes("Email Management") || content.includes("SharePoint and OneDrive"))) {
    return (
      <div className="space-y-4">
        <CapabilitiesCard data={{ capabilities: content }} />
      </div>
    );
  }

  // Check if content contains calendar card data and it's an AI response
  if (content.startsWith("CALENDAR_CARDS:") && isAiResponse) {
    try {
      const jsonData = content.replace("CALENDAR_CARDS:", "");
      const calendarData = JSON.parse(jsonData);

      // Validate that it's calendar data
      if (calendarData.Type === "calendar_events" && calendarData.Events) {
        return (
          <div className="space-y-4">
            <CalendarCard data={calendarData} />
          </div>
        );
      }
    } catch (error) {
      console.error("Failed to parse calendar data:", error);
      // Fall back to regular text rendering
    }
  }

  // Check if content contains task card data and it's an AI response
  if (content.startsWith("TASK_CARDS:") && isAiResponse) {
    try {
      const jsonData = content.replace("TASK_CARDS:", "");
      const taskData = JSON.parse(jsonData);

      // Validate that it's an array of tasks
      if (Array.isArray(taskData)) {
        return (
          <div className="space-y-4">
            <NoteCard notes={taskData} />
          </div>
        );
      } else {
        console.warn("Task data is not an array:", taskData);
      }
    } catch (error) {
      console.error("Failed to parse task data:", error);
      // Fall back to regular text rendering
    }
  }

  // Regular text content
  return <div className="whitespace-pre-line">{content}</div>;
};

export default MessageRenderer;
