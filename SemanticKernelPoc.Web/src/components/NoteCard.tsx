import React from "react";

interface TaskItem {
  id: string;
  title: string;
  content: string;
  status: string;
  priority: string;
  dueDate?: string;
  dueDateFormatted?: string;
  created: string;
  createdDateTime?: string;
  isCompleted: boolean;
  matchReason?: string;
  priorityColor: string;
  statusColor: string;
  webLink?: string;
}

interface NoteCardProps {
  notes: TaskItem[];
}

// Helper functions moved outside component for better performance
const isTaskOverdue = (dueDateStr: string | undefined) => {
  if (!dueDateStr) return false;
  
  try {
    const dueDate = new Date(dueDateStr);
    const today = new Date();
    today.setHours(0, 0, 0, 0); // Reset time to start of day for accurate comparison
    
    return dueDate < today;
  } catch {
    return false;
  }
};

const getPriorityIcon = (priority: string) => {
  switch (priority.toLowerCase()) {
    case "high":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-8.293l-3-3a1 1 0 00-1.414 0l-3 3a1 1 0 001.414 1.414L9 9.414V13a1 1 0 102 0V9.414l1.293 1.293a1 1 0 001.414-1.414z" clipRule="evenodd" />
        </svg>
      );
    case "medium":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M3 10a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
        </svg>
      );
    case "low":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-11a1 1 0 10-2 0v3.586L7.707 9.293a1 1 0 00-1.414 1.414l3 3a1 1 0 001.414 0l3-3a1 1 0 00-1.414-1.414L11 10.586V7z" clipRule="evenodd" />
        </svg>
      );
    default:
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
        </svg>
      );
  }
};

const getStatusIcon = (status: string, isCompleted: boolean) => {
  if (isCompleted) {
    return (
      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
      </svg>
    );
  }

  switch (status.toLowerCase()) {
    case "notstarted":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM7 9a1 1 0 000 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
        </svg>
      );
    case "inprogress":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
        </svg>
      );
    case "waitingonothers":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      );
    case "deferred":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
        </svg>
      );
    default:
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
        </svg>
      );
  }
};

const formatStatus = (status: string) => {
  switch (status.toLowerCase()) {
    case "notstarted":
      return "Not Started";
    case "inprogress":
      return "In Progress";
    case "completed":
      return "Completed";
    case "waitingonothers":
      return "Waiting on Others";
    case "deferred":
      return "Deferred";
    default:
      return status;
  }
};

const getNoteColor = (index: number) => {
  const colors = [
    { from: "#3B82F6", to: "#1D4ED8" }, // Blue
    { from: "#10B981", to: "#047857" }, // Green
    { from: "#F59E0B", to: "#D97706" }, // Amber
    { from: "#EF4444", to: "#DC2626" }, // Red
    { from: "#8B5CF6", to: "#7C3AED" }, // Purple
    { from: "#06B6D4", to: "#0891B2" }, // Cyan
  ];
  return colors[index % colors.length];
};

// Individual note item component for better performance
const NoteItem: React.FC<{ note: TaskItem; index: number }> = React.memo(({ note, index }) => {
  const noteColor = getNoteColor(index);
  const isOverdue = isTaskOverdue(note.dueDate);

  return (
    <div
      key={note.id}
      className="group rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
      style={{
        background: "var(--card-bg)",
        border: "1px solid var(--border-primary)",
      }}
    >
      {/* Task Color Bar */}
      <div
        className="h-1"
        style={{
          background: `linear-gradient(to right, ${noteColor.from}, ${noteColor.to})`,
        }}
        title="Task color indicator - helps distinguish between different tasks"
      ></div>

      <div className="p-4">
        {/* Task Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1 min-w-0">
            <h4 className="text-base font-semibold mb-1 transition-colors" style={{ color: "var(--text-primary)", fontWeight: "600" }} title={`Task: ${note.title}`}>
              {note.title}
            </h4>

            {note.content && note.content !== note.title && (
              <p className="text-sm mb-2 line-clamp-2" style={{ color: "var(--text-secondary)" }} title={`Task content: ${note.content}`}>
                {note.content}
              </p>
            )}

            <div className="flex items-center space-x-2 flex-wrap gap-y-1">
              {/* Overdue Badge - Less prominent color scheme */}
              {isOverdue && (
                <span
                  className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-yellow-50 text-yellow-700 border border-yellow-200"
                  title={`This task is overdue since ${note.dueDateFormatted}`}
                >
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                  </svg>
                  Overdue
                </span>
              )}

              {/* Status Badge */}
              <span
                className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                style={{
                  backgroundColor: `${note.statusColor}20`,
                  color: note.statusColor,
                  border: `1px solid ${note.statusColor}40`,
                }}
                title={`Task status: ${formatStatus(note.status)}${note.isCompleted ? " (Completed)" : ""}`}
              >
                {getStatusIcon(note.status, note.isCompleted)}
                <span className="ml-1">{formatStatus(note.status)}</span>
              </span>

              {/* Priority Badge */}
              <span
                className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                style={{
                  backgroundColor: `${note.priorityColor}20`,
                  color: note.priorityColor,
                  border: `1px solid ${note.priorityColor}40`,
                }}
                title={`Task priority: ${note.priority}`}
              >
                {getPriorityIcon(note.priority)}
                <span className="ml-1">{note.priority}</span>
              </span>

              {/* Match Reason for Search Results */}
              {note.matchReason && (
                <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-yellow-100 text-yellow-700" title={`Search match found in: ${note.matchReason}`}>
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z" clipRule="evenodd" />
                  </svg>
                  Match: {note.matchReason}
                </span>
              )}
            </div>
          </div>

          {/* Open in To Do button */}
          {note.webLink && (
            <div className="flex-shrink-0 ml-3">
              <a
                href={note.webLink}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-colors duration-200"
                style={{
                  background: "var(--accent-primary)",
                }}
                onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")}
                onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")}
                title="Open in Microsoft To Do"
              >
                <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M11 3a1 1 0 100 2h2.586l-6.293 6.293a1 1 0 101.414 1.414L15 6.414V9a1 1 0 102 0V4a1 1 0 00-1-1h-5z" />
                  <path d="M5 5a2 2 0 00-2 2v6a2 2 0 002 2h6a2 2 0 002-2v-3a1 1 0 10-2 0v3H5V7h3a1 1 0 000-2H5z" />
                </svg>
                Open
              </a>
            </div>
          )}
        </div>

        {/* Task Details */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
          {/* Due Date */}
          {note.dueDateFormatted && (
            <div className="flex items-center">
              <svg 
                className="w-4 h-4 mr-2" 
                style={{ color: "var(--text-tertiary)" }} 
                fill="currentColor" 
                viewBox="0 0 20 20"
              >
                <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
              </svg>
              <p 
                className={`font-medium text-sm ${isOverdue ? 'text-yellow-700 font-semibold' : ''}`} 
                style={{ color: isOverdue ? "#b45309" : "var(--text-primary)" }} 
                title={`Task due date: ${note.dueDateFormatted}${isOverdue ? ' (OVERDUE)' : ''}`}
              >
                Due: {note.dueDateFormatted}
              </p>
            </div>
          )}

          {/* Created Date */}
          <div className="flex items-center">
            <svg className="w-4 h-4 mr-2" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
            </svg>
            <p className="font-medium text-sm" style={{ color: "var(--text-primary)" }} title={`Task created on: ${note.created}`}>
              Created: {note.created}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
});

const NoteCard: React.FC<NoteCardProps> = ({ notes }) => {
  
  // Defensive check to prevent crash if notes is null or undefined
  if (!notes || !Array.isArray(notes)) {
    return (
      <div className="w-full max-w-2xl mx-auto">
        <div
          className="rounded-xl p-6 text-center"
          style={{
            background: "var(--bg-secondary)",
            border: "1px solid var(--border-primary)",
          }}
        >
          <div className="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3" style={{ background: "var(--bg-tertiary)" }}>
            <svg className="w-6 h-6" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 10a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 16a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
            </svg>
          </div>
          <p style={{ color: "var(--text-secondary)" }}>No task data available</p>
        </div>
      </div>
    );
  }

  if (notes.length === 0) {
    return (
      <div className="w-full max-w-2xl mx-auto">
        <div
          className="rounded-xl p-6 text-center"
          style={{
            background: "var(--bg-secondary)",
            border: "1px solid var(--border-primary)",
          }}
        >
          <div className="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3" style={{ background: "var(--bg-tertiary)" }}>
            <svg className="w-6 h-6" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 10a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 16a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
            </svg>
          </div>
          <p style={{ color: "var(--text-secondary)" }}>No tasks found</p>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Header */}
      <div
        className="mb-3 p-3 rounded-lg"
        style={{
          background: "var(--bg-secondary)",
          border: "1px solid var(--border-primary)",
        }}
        title={`Tasks overview showing ${notes.length} ${notes.length === 1 ? "task" : "tasks"}`}
      >
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center shadow-sm" style={{ background: "var(--accent-primary)" }} title="Tasks icon">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold" style={{ color: "var(--text-primary)", fontWeight: "700" }} title={`${notes.length} ${notes.length === 1 ? "task" : "tasks"} found`}>
              {notes.length === 1 ? "Task" : `${notes.length} Tasks`}
            </h3>
            <p className="text-sm" style={{ color: "var(--text-secondary)" }} title="Your recent tasks from Microsoft To Do">
              Your recent tasks
            </p>
          </div>
        </div>
      </div>

      {/* Tasks */}
      <div className="space-y-2">
        {notes.length > 0 ? (
          notes.map((note, index) => <NoteItem key={note.id || index} note={note} index={index} />)
        ) : (
          /* No tasks */
          <div
            className="rounded-xl p-6 text-center"
            style={{
              background: "var(--bg-secondary)",
              border: "1px solid var(--border-primary)",
            }}
          >
            <div className="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3" style={{ background: "var(--bg-tertiary)" }}>
              <svg className="w-6 h-6" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 10a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 16a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
              </svg>
            </div>
            <p style={{ color: "var(--text-secondary)" }}>No tasks found. Create some tasks in Microsoft To Do to see them here.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default React.memo(NoteCard);
