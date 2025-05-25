import React, { useState } from 'react';

interface MeetingTranscript {
  id: string;
  calendarEventId?: string;
  subject: string;
  startTime?: string;
  endTime?: string;
  attendeeCount: number;
  transcriptCount?: number;
  latestTranscriptId?: string;
  transcriptCreated?: string;
  hasTranscript: boolean;
  transcriptPreview?: string;
  transcript?: string;
  wordCount?: number;
}

interface MeetingTranscriptCardProps {
  transcripts: MeetingTranscript[];
  onViewFullTranscript?: (meetingId: string) => void;
  onSummarizeMeeting?: (meetingId: string) => void;
  onExtractDecisions?: (meetingId: string) => void;
  onProposeTasks?: (meetingId: string) => void;
}

const MeetingTranscriptCard: React.FC<MeetingTranscriptCardProps> = ({ 
  transcripts, 
  onViewFullTranscript,
  onSummarizeMeeting,
  onExtractDecisions,
  onProposeTasks
}) => {
  const [expandedTranscript, setExpandedTranscript] = useState<string | null>(null);

  const formatDateTime = (dateTimeString?: string) => {
    if (!dateTimeString) return 'Unknown';
    try {
      const date = new Date(dateTimeString);
      return date.toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return dateTimeString;
    }
  };

  const getDuration = (startTime?: string, endTime?: string) => {
    if (!startTime || !endTime) return null;
    try {
      const start = new Date(startTime);
      const end = new Date(endTime);
      const durationMs = end.getTime() - start.getTime();
      const minutes = Math.round(durationMs / (1000 * 60));
      return `${minutes} min`;
    } catch {
      return null;
    }
  };

  const toggleExpanded = (transcriptId: string) => {
    setExpandedTranscript(expandedTranscript === transcriptId ? null : transcriptId);
  };

  if (!transcripts || transcripts.length === 0) {
    return (
      <div 
        className="p-6 rounded-xl text-center"
        style={{
          background: 'var(--card-bg)',
          border: '1px solid var(--border-primary)'
        }}
      >
        <div className="w-16 h-16 mx-auto mb-4 rounded-full flex items-center justify-center"
             style={{ background: 'var(--bg-tertiary)' }}>
          <svg className="w-8 h-8" style={{ color: 'var(--text-tertiary)' }} fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 13V5a2 2 0 00-2-2H4a2 2 0 00-2 2v8a2 2 0 002 2h3l3 3 3-3h3a2 2 0 002-2zM5 7a1 1 0 011-1h8a1 1 0 110 2H6a1 1 0 01-1-1zm1 3a1 1 0 100 2h3a1 1 0 100-2H6z" clipRule="evenodd" />
          </svg>
        </div>
        <h3 className="text-lg font-semibold mb-2" style={{ color: 'var(--text-primary)' }}>
          No Meeting Transcripts
        </h3>
        <p className="text-sm" style={{ color: 'var(--text-secondary)' }}>
          No meeting transcripts are available. Transcripts are only generated for Teams meetings where recording/transcription is enabled.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {transcripts.map((transcript) => (
        <div
          key={transcript.id}
          className="rounded-xl shadow-sm transition-all duration-200 hover:shadow-md"
          style={{
            background: 'var(--card-bg)',
            border: '1px solid var(--border-primary)'
          }}
        >
          {/* Header */}
          <div className="p-4 border-b" style={{ borderColor: 'var(--border-primary)' }}>
            <div className="flex items-start justify-between">
              <div className="flex items-start space-x-3">
                <div 
                  className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                  style={{ background: 'var(--accent-primary)' }}
                >
                  <svg className="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M18 13V5a2 2 0 00-2-2H4a2 2 0 00-2 2v8a2 2 0 002 2h3l3 3 3-3h3a2 2 0 002-2zM5 7a1 1 0 011-1h8a1 1 0 110 2H6a1 1 0 01-1-1zm1 3a1 1 0 100 2h3a1 1 0 100-2H6z" clipRule="evenodd" />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <h3 
                    className="text-lg font-semibold mb-1 truncate"
                    style={{ color: 'var(--text-primary)' }}
                  >
                    {transcript.subject}
                  </h3>
                  <div className="flex flex-wrap items-center gap-4 text-sm" style={{ color: 'var(--text-secondary)' }}>
                    {transcript.startTime && (
                      <span className="flex items-center">
                        <svg className="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                        </svg>
                        {formatDateTime(transcript.startTime)}
                      </span>
                    )}
                    {getDuration(transcript.startTime, transcript.endTime) && (
                      <span className="flex items-center">
                        <svg className="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                        </svg>
                        {getDuration(transcript.startTime, transcript.endTime)}
                      </span>
                    )}
                    <span className="flex items-center">
                      <svg className="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20">
                        <path d="M13 6a3 3 0 11-6 0 3 3 0 016 0zM18 8a2 2 0 11-4 0 2 2 0 014 0zM14 15a4 4 0 00-8 0v3h8v-3z" />
                      </svg>
                      {transcript.attendeeCount} attendee{transcript.attendeeCount !== 1 ? 's' : ''}
                    </span>
                  </div>
                </div>
              </div>
              
              {/* Transcript Info */}
              <div className="flex flex-col items-end text-sm">
                {transcript.transcriptCreated && (
                  <span style={{ color: 'var(--text-secondary)' }}>
                    Transcript: {transcript.transcriptCreated}
                  </span>
                )}
                {transcript.transcriptCount && transcript.transcriptCount > 1 && (
                  <span style={{ color: 'var(--text-tertiary)' }}>
                    {transcript.transcriptCount} transcripts available
                  </span>
                )}
                {transcript.wordCount && (
                  <span style={{ color: 'var(--text-tertiary)' }}>
                    ~{transcript.wordCount} words
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Transcript Preview */}
          {transcript.transcriptPreview && (
            <div className="p-4">
              <div 
                className="p-3 rounded-lg text-sm leading-relaxed"
                style={{ 
                  background: 'var(--bg-secondary)',
                  border: '1px solid var(--border-secondary)'
                }}
              >
                <div style={{ color: 'var(--text-primary)' }}>
                  {expandedTranscript === transcript.id ? 
                    (transcript.transcript || transcript.transcriptPreview) : 
                    transcript.transcriptPreview
                  }
                </div>
                
                {transcript.transcript && transcript.transcript !== transcript.transcriptPreview && (
                  <button
                    onClick={() => toggleExpanded(transcript.id)}
                    className="mt-2 text-xs font-medium transition-colors"
                    style={{ color: 'var(--accent-primary)' }}
                    onMouseEnter={(e) => e.currentTarget.style.color = 'var(--accent-secondary)'}
                    onMouseLeave={(e) => e.currentTarget.style.color = 'var(--accent-primary)'}
                  >
                    {expandedTranscript === transcript.id ? 'Show less' : 'Show full transcript'}
                  </button>
                )}
              </div>
            </div>
          )}

          {/* Action Buttons */}
          <div className="p-4 border-t" style={{ borderColor: 'var(--border-primary)' }}>
            <div className="flex flex-wrap gap-2">
              {onViewFullTranscript && (
                <button
                  onClick={() => onViewFullTranscript(transcript.id)}
                  className="inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium transition-colors"
                  style={{ 
                    background: 'var(--accent-primary)10',
                    color: 'var(--accent-primary)'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = 'var(--accent-primary)20'}
                  onMouseLeave={(e) => e.currentTarget.style.background = 'var(--accent-primary)10'}
                >
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                    <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                  </svg>
                  View Full
                </button>
              )}
              
              {onSummarizeMeeting && (
                <button
                  onClick={() => onSummarizeMeeting(transcript.id)}
                  className="inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium transition-colors"
                  style={{ 
                    background: 'var(--bg-tertiary)',
                    color: 'var(--text-secondary)'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = 'var(--border-primary)'}
                  onMouseLeave={(e) => e.currentTarget.style.background = 'var(--bg-tertiary)'}
                >
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
                  </svg>
                  Summarize
                </button>
              )}
              
              {onExtractDecisions && (
                <button
                  onClick={() => onExtractDecisions(transcript.id)}
                  className="inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium transition-colors"
                  style={{ 
                    background: 'var(--bg-tertiary)',
                    color: 'var(--text-secondary)'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = 'var(--border-primary)'}
                  onMouseLeave={(e) => e.currentTarget.style.background = 'var(--bg-tertiary)'}
                >
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                  Decisions
                </button>
              )}
              
              {onProposeTasks && (
                <button
                  onClick={() => onProposeTasks(transcript.id)}
                  className="inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium transition-colors"
                  style={{ 
                    background: 'var(--bg-tertiary)',
                    color: 'var(--text-secondary)'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = 'var(--border-primary)'}
                  onMouseLeave={(e) => e.currentTarget.style.background = 'var(--bg-tertiary)'}
                >
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
                  </svg>
                  Tasks
                </button>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
};

export default MeetingTranscriptCard; 