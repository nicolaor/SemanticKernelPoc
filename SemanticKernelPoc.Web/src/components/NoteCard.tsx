import React from 'react';

interface NoteItem {
  id: string;
  title: string;
  content: string;
  status: string;
  priority: string;
  dueDate?: string;
  dueDateFormatted?: string;
  list: string;
  created: string;
  createdDateTime?: string;
  isCompleted: boolean;
  matchReason?: string;
  priorityColor: string;
  statusColor: string;
  webLink?: string;
}

interface NoteCardProps {
  notes: NoteItem[];
}

const NoteCard: React.FC<NoteCardProps> = ({ notes }) => {
  const getPriorityIcon = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'high':
        return (
          <svg className="w-3 h-3 text-red-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
        );
      case 'low':
        return (
          <svg className="w-3 h-3 text-green-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        );
      default:
        return (
          <svg className="w-3 h-3 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
          </svg>
        );
    }
  };

  const getStatusIcon = (status: string, isCompleted: boolean) => {
    if (isCompleted) {
      return (
        <svg className="w-3 h-3 text-green-600" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
        </svg>
      );
    }

    switch (status.toLowerCase()) {
      case 'inprogress':
        return (
          <svg className="w-3 h-3 text-orange-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
          </svg>
        );
      case 'waitingonothers':
        return (
          <svg className="w-3 h-3 text-purple-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-6-3a2 2 0 11-4 0 2 2 0 014 0zm-2 4a5 5 0 00-4.546 2.916A5.986 5.986 0 0010 16a5.986 5.986 0 004.546-2.084A5 5 0 0010 11z" clipRule="evenodd" />
          </svg>
        );
      case 'deferred':
        return (
          <svg className="w-3 h-3 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
        );
      default:
        return (
          <svg className="w-3 h-3 text-blue-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
          </svg>
        );
    }
  };

  const formatStatus = (status: string) => {
    switch (status.toLowerCase()) {
      case 'notstarted': return 'Not Started';
      case 'inprogress': return 'In Progress';
      case 'completed': return 'Completed';
      case 'waitingonothers': return 'Waiting on Others';
      case 'deferred': return 'Deferred';
      default: return status;
    }
  };

  const getNoteColor = (index: number) => {
    const colors = [
      'from-emerald-500 to-emerald-600',
      'from-blue-500 to-blue-600',
      'from-purple-500 to-purple-600',
      'from-orange-500 to-orange-600',
      'from-pink-500 to-pink-600',
      'from-indigo-500 to-indigo-600',
    ];
    return colors[index % colors.length];
  };

  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Header */}
      <div className="mb-3 p-3 bg-gradient-to-r from-slate-50 to-emerald-50 rounded-lg border border-slate-200/50">
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 bg-gradient-to-r from-emerald-500 to-green-600 rounded-lg flex items-center justify-center shadow-sm">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold text-slate-800">
              {notes.length === 1 ? 'Note' : `${notes.length} Notes`}
            </h3>
            <p className="text-sm text-slate-600">
              Your recent notes and tasks
            </p>
          </div>
        </div>
      </div>

      {/* Notes */}
      <div className="space-y-2">
        {notes.length > 0 ? (
          notes.map((note, index) => {
            const noteColor = getNoteColor(index);
            
            return (
              <div
                key={note.id || index}
                className="group bg-white border border-slate-200/60 rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
              >
                {/* Note Color Bar */}
                <div className={`h-1 bg-gradient-to-r ${noteColor}`}></div>
                
                <div className="p-4">
                  {/* Note Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1 min-w-0">
                      <h4 className="text-base font-semibold text-slate-900 mb-1 group-hover:text-slate-700 transition-colors">
                        {note.title}
                      </h4>
                      
                      {note.content && note.content !== note.title && (
                        <p className="text-sm text-slate-600 mb-2 line-clamp-2">
                          {note.content}
                        </p>
                      )}
                      
                      <div className="flex items-center space-x-2 flex-wrap gap-y-1">
                        {/* Status Badge */}
                        <span 
                          className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                          style={{ backgroundColor: `${note.statusColor}20`, color: note.statusColor }}
                        >
                          {getStatusIcon(note.status, note.isCompleted)}
                          <span className="ml-1">{formatStatus(note.status)}</span>
                        </span>
                        
                        {/* Priority Badge */}
                        <span 
                          className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                          style={{ backgroundColor: `${note.priorityColor}20`, color: note.priorityColor }}
                        >
                          {getPriorityIcon(note.priority)}
                          <span className="ml-1">{note.priority}</span>
                        </span>

                        {/* Match Reason for Search Results */}
                        {note.matchReason && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-yellow-100 text-yellow-700">
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
                          className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium bg-emerald-100 text-emerald-700 hover:bg-emerald-200 transition-colors duration-200"
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

                  {/* Note Details */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                    {/* Due Date */}
                    {note.dueDateFormatted && (
                      <div className="flex items-center space-x-2">
                        <div className="w-6 h-6 bg-red-100 rounded-md flex items-center justify-center">
                          <svg className="w-3 h-3 text-red-600" fill="currentColor" viewBox="0 0 20 20">
                            <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
                          </svg>
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="text-slate-900 font-medium">Due: {note.dueDateFormatted}</p>
                        </div>
                      </div>
                    )}

                    {/* List/Category */}
                    <div className="flex items-center space-x-2">
                      <div className="w-6 h-6 bg-blue-100 rounded-md flex items-center justify-center">
                        <svg className="w-3 h-3 text-blue-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-slate-900 font-medium truncate">{note.list}</p>
                      </div>
                    </div>

                    {/* Created Date */}
                    <div className="flex items-center space-x-2 md:col-span-2">
                      <div className="w-6 h-6 bg-gray-100 rounded-md flex items-center justify-center">
                        <svg className="w-3 h-3 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-slate-900 font-medium">Created: {note.created}</p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            );
          })
        ) : (
          /* No notes */
          <div className="bg-slate-50 border border-slate-200/60 rounded-xl p-6 text-center">
            <div className="w-12 h-12 bg-slate-300 rounded-full flex items-center justify-center mx-auto mb-3">
              <svg className="w-6 h-6 text-slate-500" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
              </svg>
            </div>
            <p className="text-slate-600">
              No notes found
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default NoteCard; 