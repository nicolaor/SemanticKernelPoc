import React from 'react';
import CalendarCard from './CalendarCard';
import NoteCard from './NoteCard';
import CapabilitiesCard from './CapabilitiesCard';
import MeetingTranscriptCard from './MeetingTranscriptCard';
import TaskProposalCard from './TaskProposalCard';

interface MessageRendererProps {
  content: string;
  isAiResponse?: boolean;
}

const MessageRenderer: React.FC<MessageRendererProps> = ({ content, isAiResponse = false }) => {
  // Check if content contains capabilities information and it's an AI response
  if (isAiResponse && (
    content.includes('I can assist you with a variety of tasks') ||
    content.includes('Calendar Management') ||
    content.includes('Note-Taking') ||
    content.includes('Email Management') ||
    content.includes('SharePoint and OneDrive')
  )) {
    return (
      <div className="space-y-4">
        <CapabilitiesCard data={{ capabilities: content }} />
      </div>
    );
  }

  // Check if content contains calendar card data and it's an AI response
  if (content.startsWith('CALENDAR_CARDS:') && isAiResponse) {
    try {
      const jsonData = content.replace('CALENDAR_CARDS:', '');
      const calendarData = JSON.parse(jsonData);
      
      // Validate that it's calendar data
      if (calendarData.Type === 'calendar_events' && calendarData.Events) {
        return (
          <div className="space-y-4">
            <CalendarCard data={calendarData} />
          </div>
        );
      }
    } catch (error) {
      console.error('Failed to parse calendar data:', error);
      // Fall back to regular text rendering
    }
  }

  // Check if content contains note card data and it's an AI response
  if (content.startsWith('NOTE_CARDS:') && isAiResponse) {
    try {
      const jsonData = content.replace('NOTE_CARDS:', '');
      const noteData = JSON.parse(jsonData);
      
      // Validate that it's an array of notes
      if (Array.isArray(noteData)) {
        return (
          <div className="space-y-4">
            <NoteCard notes={noteData} />
          </div>
        );
      }
    } catch (error) {
      console.error('Failed to parse note data:', error);
      // Fall back to regular text rendering
    }
  }

  // Check if content contains meeting transcript data and it's an AI response
  if (content.startsWith('MEETING_TRANSCRIPTS:') && isAiResponse) {
    try {
      const jsonData = content.replace('MEETING_TRANSCRIPTS:', '');
      const meetingData = JSON.parse(jsonData);
      
      // Validate that it's meeting transcript data
      if (meetingData.Type === 'meeting_transcripts' && meetingData.Meetings) {
        return (
          <div className="space-y-4">
            <MeetingTranscriptCard transcripts={meetingData.Meetings || []} />
          </div>
        );
      }
    } catch (error) {
      console.error('Failed to parse meeting transcript data:', error);
      // Fall back to regular text rendering
    }
  }

  // Check if content contains task proposals and it's an AI response
  if (content.startsWith('TASK_PROPOSALS:') && isAiResponse) {
    try {
      const jsonData = content.replace('TASK_PROPOSALS:', '');
      const taskProposals = JSON.parse(jsonData);
      
      // Validate that it's an array of task proposals
      if (Array.isArray(taskProposals)) {
        return (
          <div className="space-y-4">
            <TaskProposalCard 
              proposals={taskProposals} 
              onCreateTasks={(tasks) => {
                // TODO: Implement task creation functionality
                console.log('Creating tasks:', tasks);
              }} 
            />
          </div>
        );
      }
    } catch (error) {
      console.error('Failed to parse task proposal data:', error);
      // Fall back to regular text rendering
    }
  }

  // Regular text content
  return (
    <div className="whitespace-pre-line">
      {content}
    </div>
  );
};

export default MessageRenderer; 