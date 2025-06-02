import React, { useState, useEffect } from "react";
import { createPortal } from "react-dom";

interface AttendeeInfo {
  Name: string;
  Email: string;
  ResponseStatus: string;
}

interface CalendarEvent {
  subject: string;
  start: string | null;
  end: string | null;
  location: string;
  organizer: string;
  isAllDay: boolean;
  id: string;
  attendeeCount?: number;
  webLink?: string;
  attendees?: AttendeeInfo[];
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
  const [showAttendees, setShowAttendees] = useState<string | null>(null);
  const [bubblePosition, setBubblePosition] = useState<"top" | "bottom">("bottom");
  const [buttonRect, setButtonRect] = useState<DOMRect | null>(null);
  const formatDateTime = (dateStr: string | null | undefined) => {
    if (!dateStr) {
      return {
        date: "Unknown Date",
        time: "Unknown Time",
      };
    }
    
    try {
      const date = new Date(dateStr);
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return {
          date: "Invalid Date",
          time: "Invalid Time",
        };
      }
      
      return {
        date: date.toLocaleDateString("en-US", {
          weekday: "short",
          month: "short",
          day: "numeric",
        }),
        time: date.toLocaleTimeString("en-US", {
          hour: "numeric",
          minute: "2-digit",
          hour12: true,
        }),
      };
    } catch (error) {
      return {
        date: "Invalid Date",
        time: "Invalid Time",
      };
    }
  };

  const getEventDuration = (start: string | null | undefined, end: string | null | undefined) => {
    if (!start || !end) {
      return "Unknown Duration";
    }
    
    try {
      const startDate = new Date(start);
      const endDate = new Date(end);
      
      // Check if dates are valid
      if (isNaN(startDate.getTime()) || isNaN(endDate.getTime())) {
        return "Invalid Duration";
      }
      
      const diffMs = endDate.getTime() - startDate.getTime();
      
      // Check for negative duration
      if (diffMs < 0) {
        return "Invalid Duration";
      }
      
      const diffMins = Math.round(diffMs / (1000 * 60));

      if (diffMins < 60) {
        return `${diffMins}m`;
      } else {
        const hours = Math.floor(diffMins / 60);
        const minutes = diffMins % 60;
        return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
      }
    } catch (error) {
      return "Invalid Duration";
    }
  };

  const getEventColor = (index: number) => {
    const colors = [
      { from: "#3b82f6", to: "#2563eb" }, // blue
      { from: "#10b981", to: "#059669" }, // emerald
      { from: "#8b5cf6", to: "#7c3aed" }, // purple
      { from: "#f97316", to: "#ea580c" }, // orange
      { from: "#ec4899", to: "#db2777" }, // pink
      { from: "#6366f1", to: "#4f46e5" }, // indigo
    ];
    return colors[index % colors.length];
  };

  const handleAttendeeClick = (eventId: string, buttonElement: HTMLButtonElement) => {
    if (showAttendees === eventId) {
      setShowAttendees(null);
      setButtonRect(null);
      return;
    }

    // Calculate available space above and below the button
    const rect = buttonElement.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const spaceAbove = rect.top;
    const spaceBelow = viewportHeight - rect.bottom;

    // Estimate bubble height (roughly 200px for content + padding)
    const estimatedBubbleHeight = 200;

    // Choose position based on available space
    if (spaceBelow >= estimatedBubbleHeight || spaceBelow >= spaceAbove) {
      setBubblePosition("bottom");
    } else {
      setBubblePosition("top");
    }

    // Store button position for portal positioning
    setButtonRect(rect);
    setShowAttendees(eventId);
  };

  // Close bubble on scroll, resize, or click outside
  useEffect(() => {
    const handleScrollOrResize = () => {
      if (showAttendees) {
        setShowAttendees(null);
        setButtonRect(null);
      }
    };

    const handleClickOutside = (event: MouseEvent) => {
      if (showAttendees) {
        // Check if the click is outside the bubble and not on the attendee button
        const target = event.target as Element;
        const bubble = document.querySelector("[data-attendee-bubble]");
        const attendeeButton = document.querySelector(`[data-attendee-button="${showAttendees}"]`);

        if (bubble && !bubble.contains(target) && attendeeButton && !attendeeButton.contains(target)) {
          setShowAttendees(null);
          setButtonRect(null);
        }
      }
    };

    if (showAttendees) {
      window.addEventListener("scroll", handleScrollOrResize, true);
      window.addEventListener("resize", handleScrollOrResize);
      document.addEventListener("mousedown", handleClickOutside);
    }

    return () => {
      window.removeEventListener("scroll", handleScrollOrResize, true);
      window.removeEventListener("resize", handleScrollOrResize);
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, [showAttendees]);

  // Portal bubble component
  const AttendeeBubble = ({ event }: { event: CalendarEvent }) => {
    if (!buttonRect || !event.attendees || event.attendees.length === 0) return null;

    const bubbleStyle: React.CSSProperties = {
      position: "fixed",
      left: buttonRect.left,
      width: "288px", // w-72 = 18rem = 288px
      zIndex: 9999,
    };

    if (bubblePosition === "top") {
      bubbleStyle.bottom = window.innerHeight - buttonRect.top + 8; // 8px margin
    } else {
      bubbleStyle.top = buttonRect.bottom + 8; // 8px margin
    }

    return createPortal(
      <div
        style={{
          ...bubbleStyle,
          background: "var(--card-bg)",
          border: "1px solid var(--border-primary)",
          boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
        }}
        className="rounded-lg p-3"
        data-attendee-bubble
      >
        <div className="flex items-center justify-between mb-2">
          <h4 className="text-sm font-semibold" style={{ color: "var(--text-primary)" }}>
            Attendees
          </h4>
          <button
            onClick={() => {
              setShowAttendees(null);
              setButtonRect(null);
            }}
            className="transition-colors"
            style={{ color: "var(--text-tertiary)" }}
            onMouseEnter={(e) => (e.currentTarget.style.color = "var(--text-secondary)")}
            onMouseLeave={(e) => (e.currentTarget.style.color = "var(--text-tertiary)")}
          >
            <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
            </svg>
          </button>
        </div>
        <div className="space-y-2 max-h-40 overflow-y-auto">
          {event.attendees.map((attendee, idx) => (
            <div key={idx} className="flex items-center space-x-2 p-2 rounded" style={{ background: "var(--bg-secondary)" }}>
              <div className="w-6 h-6 rounded-full flex items-center justify-center" style={{ background: "var(--accent-primary)20" }}>
                <svg className="w-3 h-3 text-purple-500" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M10 9a3 3 0 100-6 3 3 0 000 6zm-7 9a7 7 0 1114 0H3z" clipRule="evenodd" />
                </svg>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium truncate" style={{ color: "var(--text-primary)" }}>
                  {attendee.Name || attendee.Email}
                </p>
                {attendee.Name && attendee.Email && attendee.Name !== attendee.Email && (
                  <p className="text-xs truncate" style={{ color: "var(--text-tertiary)" }}>
                    {attendee.Email}
                  </p>
                )}
              </div>
              <div className="flex-shrink-0">
                <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${attendee.ResponseStatus === "Accepted" ? "bg-green-100 text-green-800" : attendee.ResponseStatus === "Declined" ? "bg-red-100 text-red-800" : attendee.ResponseStatus === "Tentative" ? "bg-yellow-100 text-yellow-800" : "bg-gray-100 text-gray-800"}`}>{attendee.ResponseStatus === "None" ? "Pending" : attendee.ResponseStatus}</span>
              </div>
            </div>
          ))}
        </div>
      </div>,
      document.body,
    );
  };

  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Compact Header */}
      <div
        className="mb-3 p-3 rounded-lg"
        style={{
          background: "var(--bg-secondary)",
          border: "1px solid var(--border-primary)",
        }}
        title={`Calendar overview showing ${data.Count} ${data.Count === 1 ? "event" : "events"} for ${data.TimeRange}`}
      >
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center shadow-sm" style={{ background: "var(--accent-primary)" }} title="Calendar icon">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold" style={{ color: "var(--text-primary)", fontWeight: "700" }} title={`${data.Count} ${data.Count === 1 ? "calendar event" : "calendar events"} found`}>
              {data.Count === 1 ? "Calendar Event" : `${data.Count} Events`}
            </h3>
            <p className="text-sm" style={{ color: "var(--text-secondary)" }} title={`Time period: ${data.TimeRange} | User: ${data.UserName}`}>
              {data.TimeRange} • {data.UserName}
            </p>
          </div>
        </div>
      </div>

      {/* Compact Events */}
      <div className="space-y-2">
        {data.Events && data.Events.length > 0 ? (
          data.Events.map((event, index) => {
            const startFormat = formatDateTime(event.start);
            const endFormat = formatDateTime(event.end);
            const duration = getEventDuration(event.start, event.end);
            const isSameDay = startFormat.date === endFormat.date;
            const eventColor = getEventColor(index);

            return (
              <div
                key={event.id || index}
                className="group rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
                style={{
                  background: "var(--card-bg)",
                  border: "1px solid var(--border-primary)",
                }}
              >
                {/* Event Color Bar */}
                <div
                  className="h-1"
                  style={{
                    background: `linear-gradient(to right, ${eventColor.from}, ${eventColor.to})`,
                  }}
                  title="Event color indicator - helps distinguish between different events"
                ></div>

                <div className="p-4">
                  {/* Event Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1 min-w-0">
                      <h4
                        className="text-base font-semibold mb-1 truncate transition-colors"
                        style={{
                          color: "var(--text-primary)",
                          fontWeight: "600",
                        }}
                        title={`Event: ${event.subject}`}
                      >
                        {event.subject}
                      </h4>

                      {/* Duration on dedicated line */}
                      <div className="mb-2">
                        <span className="text-sm font-medium" style={{ color: "var(--text-secondary)" }} title={`Event duration: ${duration} (from ${formatDateTime(event.start).time} to ${formatDateTime(event.end).time})`}>
                          Duration: {duration}
                        </span>
                      </div>

                      <div className="flex items-center space-x-2 flex-wrap gap-y-1">
                        {event.isAllDay && (
                          <span
                            className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                            style={{
                              background: "var(--bg-tertiary)",
                              color: "var(--text-primary)",
                            }}
                            title="This is an all-day event with no specific start/end times"
                          >
                            All Day
                          </span>
                        )}
                        {event.attendeeCount && event.attendeeCount > 0 && (
                          <button
                            onClick={(e) => handleAttendeeClick(event.id, e.currentTarget)}
                            className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium transition-colors cursor-pointer"
                            style={{
                              background: "var(--bg-tertiary)",
                              color: "var(--text-primary)",
                            }}
                            onMouseEnter={(e) => (e.currentTarget.style.background = "var(--border-primary)")}
                            onMouseLeave={(e) => (e.currentTarget.style.background = "var(--bg-tertiary)")}
                            title="Click to see attendees"
                            data-attendee-button={event.id}
                          >
                            <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                              <path d="M13 6a3 3 0 11-6 0 3 3 0 016 0zM18 8a2 2 0 11-4 0 2 2 0 014 0zM14 15a4 4 0 00-8 0v3h8v-3z" />
                            </svg>
                            {event.attendeeCount} attendee
                            {event.attendeeCount !== 1 ? "s" : ""}
                          </button>
                        )}
                      </div>
                    </div>

                    {/* Open in Outlook button */}
                    {event.webLink && (
                      <div className="flex-shrink-0 ml-3">
                        <a href={event.webLink} target="_blank" rel="noopener noreferrer" className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-colors duration-200" style={{ background: "var(--accent-primary)" }} onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")} onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")} title="Open in Outlook">
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
                      <div className="w-6 h-6 rounded-md flex items-center justify-center" style={{ background: "var(--bg-tertiary)" }} title="Event date and time">
                        <svg className="w-3 h-3 text-blue-500" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        {event.isAllDay ? (
                          <p className="font-medium" style={{ color: "var(--text-primary)" }} title={`All-day event on ${startFormat.date}`}>
                            {startFormat.date}
                          </p>
                        ) : (
                          <div>
                            <p className="font-medium" style={{ color: "var(--text-primary)" }} title={`Event date: ${startFormat.date}`}>
                              {startFormat.date}
                            </p>
                            <p className="text-xs" style={{ color: "var(--text-secondary)" }} title={`Event time: ${startFormat.time} to ${isSameDay ? endFormat.time : `${endFormat.date} ${endFormat.time}`}`}>
                              {startFormat.time} → {isSameDay ? endFormat.time : `${endFormat.date} ${endFormat.time}`}
                            </p>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Location */}
                    <div className="flex items-center space-x-2">
                      <div className="w-6 h-6 rounded-md flex items-center justify-center" style={{ background: "var(--bg-tertiary)" }} title="Event location">
                        <svg className="w-3 h-3 text-red-500" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M5.05 4.05a7 7 0 119.9 9.9L10 18.9l-4.95-4.95a7 7 0 010-9.9zM10 11a2 2 0 100-4 2 2 0 000 4z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="font-medium truncate" style={{ color: "var(--text-primary)" }} title={!event.location || event.location === "No location" || event.location.trim() === "" ? "No location specified for this event" : `Event location: ${event.location}`}>
                          {!event.location || event.location === "No location" || event.location.trim() === "" ? (
                            <span className="italic" style={{ color: "var(--text-tertiary)" }}>
                              no location
                            </span>
                          ) : (
                            event.location
                          )}
                        </p>
                      </div>
                    </div>

                    {/* Organizer */}
                    <div className="flex items-center space-x-2 md:col-span-2">
                      <div className="w-6 h-6 rounded-md flex items-center justify-center" style={{ background: "var(--bg-tertiary)" }} title="Event organizer">
                        <svg className="w-3 h-3 text-purple-500" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 9a3 3 0 100-6 3 3 0 000 6zm-7 9a7 7 0 1114 0H3z" clipRule="evenodd" />
                        </svg>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="font-medium truncate" style={{ color: "var(--text-primary)" }} title={event.organizer === "Unknown" ? "Event organizer information is not available" : `Event organized by: ${event.organizer}`}>
                          {event.organizer === "Unknown" ? (
                            <span className="italic" style={{ color: "var(--text-tertiary)" }}>
                              👤 Organizer not specified
                            </span>
                          ) : (
                            event.organizer
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
              {data.Count} {data.Count === 1 ? "Event" : "Events"}
            </h4>
            <p className="text-slate-600 mb-4">
              You have {data.Count} calendar {data.Count === 1 ? "event" : "events"} for {data.TimeRange}
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
            <p className="text-slate-600">No events found for {data.TimeRange}</p>
          </div>
        )}
      </div>

      {/* Portal bubble for attendees */}
      {showAttendees && data.Events && <AttendeeBubble event={data.Events.find((e) => e.id === showAttendees)!} />}
    </div>
  );
};

export default CalendarCard;
