import React from 'react';

interface CalendarEvent {
  Subject: string;
  Start: string;
  End: string;
  Location: string;
  Organizer: string;
  IsAllDay: boolean;
  Id: string;
  AttendeeCount?: number;
  WebLink?: string;
}

interface CalendarData {
  Type: string;
  Count: number;
  UserName: string;
  TimeRange: string;
  Events: CalendarEvent[];
}

interface CalendarCardProps {
  data: CalendarData;
}

const CalendarCard: React.FC<CalendarCardProps> = ({ data }) => {
  const formatDateTime = (dateStr: string) => {
    const date = new Date(dateStr);
    return {
      date: date.toLocaleDateString('en-US', { 
        weekday: 'short', 
        month: 'short', 
        day: 'numeric' 
      }),
      time: date.toLocaleTimeString('en-US', { 
        hour: 'numeric', 
        minute: '2-digit',
        hour12: true 
      })
    };
  };

  const getEventDuration = (start: string, end: string) => {
    const startDate = new Date(start);
    const endDate = new Date(end);
    const diffMs = endDate.getTime() - startDate.getTime();
    const diffMins = Math.round(diffMs / (1000 * 60));
    
    if (diffMins < 60) {
      return `${diffMins}m`;
    } else {
      const hours = Math.floor(diffMins / 60);
      const minutes = diffMins % 60;
      return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
    }
  };

  const getEventColor = (index: number) => {
    const colors = [
      { from: '#3b82f6', to: '#2563eb' }, // blue
      { from: '#10b981', to: '#059669' }, // emerald
      { from: '#8b5cf6', to: '#7c3aed' }, // purple
      { from: '#f97316', to: '#ea580c' }, // orange
      { from: '#ec4899', to: '#db2777' }, // pink
      { from: '#6366f1', to: '#4f46e5' }, // indigo
    ];
    return colors[index % colors.length];
  };

  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Compact Header */}
      <div className="mb-3 p-3 bg-gradient-to-r from-slate-50 to-blue-50 rounded-lg border border-slate-200/50">
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-indigo-600 rounded-lg flex items-center justify-center shadow-sm">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold text-slate-800">
              {data.Count === 1 ? 'Calendar Event' : `${data.Count} Events`}
            </h3>
            <p className="text-sm text-slate-600">
              {data.TimeRange} • {data.UserName}
            </p>
          </div>
        </div>
      </div>

      {/* Compact Events */}
      <div className="space-y-2">
        {data.Events && data.Events.length > 0 ? (
          data.Events.map((event, index) => {
            const startFormat = formatDateTime(event.Start);
            const endFormat = formatDateTime(event.End);
            const duration = getEventDuration(event.Start, event.End);
            const isSameDay = startFormat.date === endFormat.date;
            const eventColor = getEventColor(index);
            
            return (
              <div
                key={event.Id || index}
                className="group bg-white border border-slate-200/60 rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
              >
                {/* Event Color Bar */}
                <div 
                  className="h-1"
                  style={{
                    background: `linear-gradient(to right, ${eventColor.from}, ${eventColor.to})`
                  }}
                ></div>
                
                <div className="p-4">
                  {/* Event Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1 min-w-0">
                      <h4 className="text-base font-semibold text-slate-900 mb-1 truncate group-hover:text-slate-700 transition-colors">
                        {event.Subject}
                      </h4>
                      
                      {/* Duration on dedicated line */}
                      <div className="mb-2">
                        <span className="text-sm text-slate-600 font-medium">
                          Duration: {duration}
                        </span>
                      </div>
                      
                      <div className="flex items-center space-x-2 flex-wrap gap-y-1">
                        {event.IsAllDay && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-emerald-100 text-emerald-700">
                            All Day
                          </span>
                        )}
                        {event.AttendeeCount && event.AttendeeCount > 0 && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-purple-100 text-purple-700">
                            {event.AttendeeCount} attendee{event.AttendeeCount !== 1 ? 's' : ''}
                          </span>
                        )}
                      </div>
                    </div>
                    
                    {/* Open in Outlook button */}
                    {event.WebLink && (
                      <div className="flex-shrink-0 ml-3">
                        <a
                          href={event.WebLink}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium bg-blue-100 text-blue-700 hover:bg-blue-200 transition-colors duration-200"
                          title="Open in Outlook"
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

                  {/* Compact Event Details */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                    {/* Date & Time */}
                    <div className="flex items-center space-x-2">
                      <div className="w-6 h-6 bg-blue-100 rounded-md flex items-center justify-center">
                        <svg className="w-3 h-3 text-blue-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        {event.IsAllDay ? (
                          <p className="text-slate-900 font-medium">{startFormat.date}</p>
                        ) : (
                          <div>
                            <p className="text-slate-900 font-medium">{startFormat.date}</p>
                            <p className="text-slate-600 text-xs">
                              {startFormat.time} → {isSameDay ? endFormat.time : `${endFormat.date} ${endFormat.time}`}
                            </p>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Location */}
                    <div className="flex items-center space-x-2">
                      <div className="w-6 h-6 bg-emerald-100 rounded-md flex items-center justify-center">
                        <svg className="w-3 h-3 text-emerald-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M5.05 4.05a7 7 0 119.9 9.9L10 18.9l-4.95-4.95a7 7 0 010-9.9zM10 11a2 2 0 100-4 2 2 0 000 4z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-slate-900 font-medium truncate">
                          {!event.Location || event.Location === 'No location' || event.Location.trim() === '' ? (
                            <span className="text-slate-500 italic">no location</span>
                          ) : (
                            event.Location
                          )}
                        </p>
                      </div>
                    </div>

                    {/* Organizer */}
                    <div className="flex items-center space-x-2 md:col-span-2">
                      <div className="w-6 h-6 bg-purple-100 rounded-md flex items-center justify-center">
                        <svg className="w-3 h-3 text-purple-600" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 9a3 3 0 100-6 3 3 0 000 6zm-7 9a7 7 0 1114 0H3z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-slate-900 font-medium truncate">
                          {event.Organizer === 'Unknown' ? (
                            <span className="text-slate-500 italic">👤 Organizer not specified</span>
                          ) : (
                            event.Organizer
                          )}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            );
          })
        ) : data.Count > 0 ? (
          /* Count-only display when events array is empty but count > 0 */
          <div className="bg-white border border-slate-200/60 rounded-xl shadow-sm p-6 text-center">
            <div className="w-16 h-16 bg-gradient-to-r from-blue-500 to-indigo-600 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg className="w-8 h-8 text-white" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
              </svg>
            </div>
            <h4 className="text-xl font-bold text-slate-900 mb-2">
              {data.Count} {data.Count === 1 ? 'Event' : 'Events'}
            </h4>
            <p className="text-slate-600 mb-4">
              You have {data.Count} calendar {data.Count === 1 ? 'event' : 'events'} for {data.TimeRange}
            </p>
            <div className="inline-flex items-center px-4 py-2 rounded-lg bg-blue-50 text-blue-700 text-sm font-medium">
              <svg className="w-4 h-4 mr-2" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
              </svg>
              Count Only - Ask for details to see event information
            </div>
          </div>
        ) : (
          /* No events */
          <div className="bg-slate-50 border border-slate-200/60 rounded-xl p-6 text-center">
            <div className="w-12 h-12 bg-slate-300 rounded-full flex items-center justify-center mx-auto mb-3">
              <svg className="w-6 h-6 text-slate-500" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
              </svg>
            </div>
            <p className="text-slate-600">
              No events found for {data.TimeRange}
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default CalendarCard; 