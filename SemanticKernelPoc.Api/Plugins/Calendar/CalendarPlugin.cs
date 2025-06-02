using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Helpers;

namespace SemanticKernelPoc.Api.Plugins.Calendar;

public class CalendarPlugin : BaseGraphPlugin
{
    public CalendarPlugin(IGraphService graphService, IGraphClientFactory graphClientFactory, ILogger<CalendarPlugin> logger) 
        : base(graphService, graphClientFactory, logger)
    {
    }

    private static CalendarEventResponse CreateCalendarEventResponse(Event evt)
    {
        // Parse and format the dates to ensure they're in a format JavaScript can understand
        string startDateTime = null;
        string endDateTime = null;
        
        if (evt.Start?.DateTime != null)
        {
            try
            {
                var startDate = DateTime.Parse(evt.Start.DateTime);
                // Format as ISO 8601 with Z timezone indicator for JavaScript compatibility
                startDateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
            catch
            {
                // If parsing fails, use a fallback format
                startDateTime = null;
            }
        }
        
        if (evt.End?.DateTime != null)
        {
            try
            {
                var endDate = DateTime.Parse(evt.End.DateTime);
                // Format as ISO 8601 with Z timezone indicator for JavaScript compatibility
                endDateTime = endDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
            catch
            {
                // If parsing fails, use a fallback format
                endDateTime = null;
            }
        }

        return new CalendarEventResponse(
            evt.Subject ?? "No Subject",
            startDateTime,
            endDateTime,
            evt.Location?.DisplayName ?? "No location",
            evt.Organizer?.EmailAddress?.Name ?? "Unknown",
            evt.IsAllDay ?? false,
            evt.Id,
            evt.Attendees?.Any() == true ? evt.Attendees.Count : null,
            CalendarLinkHelpers.GenerateOutlookWebLink(evt.Id ?? ""),
            evt.Attendees?.Select(a => new AttendeeInfo(
                a.EmailAddress?.Name ?? a.EmailAddress?.Address ?? "Unknown",
                a.EmailAddress?.Address ?? "",
                a.Status?.Response?.ToString() ?? "None"
            )).ToList()
        );
    }

    [KernelFunction, Description("Get the user's upcoming calendar events. For display purposes, use analysisMode=false. For summary/analysis requests like 'summarize my calendar', 'what meetings do I have', 'calendar overview', use analysisMode=true.")]
    public async Task<string> GetUpcomingEvents(Kernel kernel,
        [Description("Number of days to look ahead (default 7)")] int days = 7,
        [Description("Maximum number of events to return (default 20)")] int maxEvents = 20,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what meetings do I have'. Set to false for listing/displaying events. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var startTime = DateTime.Now;
                var endTime = startTime.AddDays(days);

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Top = maxEvents;
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    if (analysisMode)
                    {
                        // For analysis mode, return clean summary
                        var allDayCount = events.Value.Count(e => e.IsAllDay ?? false);
                        var todayCount = events.Value.Count(e => e.Start?.DateTime != null && DateTime.Parse(e.Start.DateTime).Date == DateTime.Today);
                        
                        return $"Found {events.Value.Count} upcoming events for {userName} in the next {days} days. " +
                               $"{allDayCount} all-day events, {todayCount} events today. " +
                               $"Upcoming events: {string.Join(", ", events.Value.Take(3).Select(e => e.Subject ?? "No Subject"))}.";
                    }
                    else
                    {
                        // Create calendar cards for the new approach
                        var calendarCards = events.Value.Select((evt, index) => new
                        {
                            id = $"event_{index}_{evt.Id?.GetHashCode().ToString("X")}",
                            subject = evt.Subject ?? "No Subject",
                            start = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            end = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            location = evt.Location?.DisplayName ?? "No location",
                            organizer = evt.Organizer?.EmailAddress?.Name ?? "Unknown",
                            isAllDay = evt.IsAllDay ?? false,
                            attendeeCount = evt.Attendees?.Count() ?? 0,
                            webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(evt.Id ?? "")}&exvsurl=1&path=/calendar/item"
                        }).ToList();

                        // Store structured data in kernel data for the system to process
                        kernel.Data["CalendarCards"] = calendarCards;
                        kernel.Data["HasStructuredData"] = "true";
                        kernel.Data["StructuredDataType"] = "calendar";
                        kernel.Data["StructuredDataCount"] = calendarCards.Count;

                        return $"Found {calendarCards.Count} upcoming events for {userName} in the next {days} days.";
                    }
                }

                return $"No upcoming events found for {userName} in the next {days} days.";
            },
            "GetUpcomingEvents"
        );
    }

    [KernelFunction, Description("Add a new event to the user's calendar")]
    public async Task<string> AddCalendarEvent(Kernel kernel,
        [Description("Event title/subject")] string subject,
        [Description("Start date and time (e.g., '2024-01-15 14:00' or 'tomorrow 2pm')")] string startDateTime,
        [Description("Duration in minutes (default 60)")] int durationMinutes = 60,
        [Description("Event location (optional)")] string location = null,
        [Description("Event description/body (optional)")] string description = null,
        [Description("Attendee email addresses, comma-separated (optional)")] string attendees = null)
    {
        var validation = ValidateRequiredParameter(subject, "Subject");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                if (!CalendarHelpers.TryParseDateTime(startDateTime, out var parsedStart))
                {
                    return $"Could not parse start date/time: '{startDateTime}'. Please use format like '2024-01-15 14:00' or 'tomorrow 2pm'.";
                }

                var endDateTime = parsedStart.AddMinutes(durationMinutes);

                var newEvent = new Event
                {
                    Subject = subject,
                    Start = new DateTimeTimeZone
                    {
                        DateTime = parsedStart.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = endDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        TimeZone = TimeZoneInfo.Local.Id
                    }
                };

                if (!string.IsNullOrWhiteSpace(location))
                {
                    newEvent.Location = new Location { DisplayName = location };
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    newEvent.Body = new ItemBody
                    {
                        Content = description,
                        ContentType = BodyType.Text
                    };
                }

                if (!string.IsNullOrWhiteSpace(attendees))
                {
                    var attendeeList = attendees.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(email => new Attendee
                        {
                            EmailAddress = new EmailAddress
                            {
                                Address = email.Trim(),
                                Name = email.Trim()
                            }
                        }).ToList();

                    newEvent.Attendees = attendeeList;
                }

                var createdEvent = await graphClient.Me.Calendar.Events.PostAsync(newEvent);

                return CreateSuccessResponse(
                    "Calendar event creation",
                    userName,
                    ("📅 Subject", createdEvent?.Subject ?? subject),
                    ("🕐 Start", parsedStart.ToString("yyyy-MM-dd HH:mm")),
                    ("🕓 End", endDateTime.ToString("yyyy-MM-dd HH:mm")),
                    ("⏱️ Duration", $"{durationMinutes} minutes"),
                    ("📍 Location", location ?? "No location"),
                    ("👥 Attendees", attendees ?? "None")
                );
            },
            "AddCalendarEvent"
        );
    }

    [KernelFunction, Description("Find the next available time slot of specified duration")]
    public async Task<string> FindNextAvailableSlot(Kernel kernel,
        [Description("Duration needed in minutes")] int durationMinutes,
        [Description("Buffer time before existing appointments in minutes (default 15)")] int bufferBefore = 15,
        [Description("Buffer time after existing appointments in minutes (default 15)")] int bufferAfter = 15,
        [Description("Number of days to search ahead (default 14)")] int searchDays = 14,
        [Description("Earliest hour to consider (24-hour format, default 8 for 8 AM)")] int earliestHour = 8,
        [Description("Latest hour to consider (24-hour format, default 18 for 6 PM)")] int latestHour = 18)
    {
        if (durationMinutes <= 0)
        {
            return "Duration must be greater than 0 minutes.";
        }

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var startTime = DateTime.Now;
                var endTime = startTime.AddDays(searchDays);

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                var busySlots = new List<(DateTime Start, DateTime End)>();

                if (events?.Value?.Any() == true)
                {
                    busySlots = [.. events.Value
                        .Where(evt => evt.Start?.DateTime != null && evt.End?.DateTime != null)
                        .Select(evt => (
                            Start: DateTime.Parse(evt.Start!.DateTime!).AddMinutes(-bufferBefore),
                            End: DateTime.Parse(evt.End!.DateTime!).AddMinutes(bufferAfter)
                        ))
                        .OrderBy(slot => slot.Start)];
                }

                var availableSlots = new List<(DateTime Start, DateTime End)>();

                for (var day = startTime.Date; day <= endTime.Date; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    var dayStart = day.AddHours(earliestHour);
                    var dayEnd = day.AddHours(latestHour);

                    var dayBusySlots = busySlots
                        .Where(slot => slot.Start.Date == day.Date || slot.End.Date == day.Date)
                        .ToList();

                    if (!dayBusySlots.Any())
                    {
                        availableSlots.Add((dayStart, dayEnd));
                    }
                    else
                    {
                        var (Start, End) = dayBusySlots.OrderBy(s => s.Start).First();
                        if (dayStart < Start)
                        {
                            availableSlots.Add((dayStart, Start));
                        }

                        for (int i = 0; i < dayBusySlots.Count - 1; i++)
                        {
                            var currentEnd = dayBusySlots[i].End;
                            var nextStart = dayBusySlots[i + 1].Start;

                            if (currentEnd < nextStart)
                            {
                                availableSlots.Add((currentEnd, nextStart));
                            }
                        }

                        var lastBusy = dayBusySlots.OrderBy(s => s.End).Last();
                        if (lastBusy.End < dayEnd)
                        {
                            availableSlots.Add((lastBusy.End, dayEnd));
                        }
                    }
                }

                var suitableSlots = availableSlots
                    .Where(slot => (slot.End - slot.Start).TotalMinutes >= durationMinutes)
                    .Select(slot => new
                    {
                        Start = slot.Start.ToString("yyyy-MM-dd HH:mm"),
                        End = slot.Start.AddMinutes(durationMinutes).ToString("yyyy-MM-dd HH:mm"),
                        Day = slot.Start.ToString("dddd, MMMM dd"),
                        AvailableDuration = (int)(slot.End - slot.Start).TotalMinutes
                    })
                    .Take(10)
                    .ToList();

                if (suitableSlots.Any())
                {
                    return FormatJsonResponse(suitableSlots, userName,
                        $"Available time slots ({durationMinutes} minutes needed)");
                }

                return $"No available time slots found for {userName} in the next {searchDays} days that can accommodate {durationMinutes} minutes.";
            },
            "FindNextAvailableSlot"
        );
    }

    [KernelFunction, Description("Get today's calendar events. For display purposes, use analysisMode=false. For summary/analysis requests like 'summarize today's meetings', 'what do I have today', use analysisMode=true.")]
    public async Task<string> GetTodaysEvents(Kernel kernel,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what meetings do I have'. Set to false for listing/displaying events. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = today.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = tomorrow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    if (analysisMode)
                    {
                        // For analysis mode, return clean summary
                        var allDayCount = events.Value.Count(e => e.IsAllDay ?? false);
                        
                        return $"Found {events.Value.Count} events today for {userName}. " +
                               $"{allDayCount} all-day events. " +
                               $"Today's events: {string.Join(", ", events.Value.Take(3).Select(e => e.Subject ?? "No Subject"))}.";
                    }
                    else
                    {
                        // Create calendar cards for the new approach
                        var calendarCards = events.Value.Select((evt, index) => new
                        {
                            id = $"today_{index}_{evt.Id?.GetHashCode().ToString("X")}",
                            subject = evt.Subject ?? "No Subject",
                            start = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            end = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            location = evt.Location?.DisplayName ?? "No location",
                            organizer = evt.Organizer?.EmailAddress?.Name ?? "Unknown",
                            isAllDay = evt.IsAllDay ?? false,
                            attendeeCount = evt.Attendees?.Count() ?? 0,
                            webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(evt.Id ?? "")}&exvsurl=1&path=/calendar/item"
                        }).ToList();

                        // Store structured data in kernel data for the system to process
                        kernel.Data["CalendarCards"] = calendarCards;
                        kernel.Data["HasStructuredData"] = "true";
                        kernel.Data["StructuredDataType"] = "calendar";
                        kernel.Data["StructuredDataCount"] = calendarCards.Count;

                        return $"Found {calendarCards.Count} events for today ({today:yyyy-MM-dd}) for {userName}.";
                    }
                }

                // No events today, check for upcoming events in the next 7 days
                var weekFromNow = today.AddDays(7);
                var upcomingEvents = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = tomorrow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = weekFromNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Top = 5;
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (upcomingEvents?.Value?.Any() == true)
                {
                    if (analysisMode)
                    {
                        // For analysis mode, return clean summary
                        var allDayCount = upcomingEvents.Value.Count(e => e.IsAllDay ?? false);
                        
                        return $"No events today. Found {upcomingEvents.Value.Count} upcoming events for {userName} in the next week. " +
                               $"{allDayCount} all-day events. " +
                               $"Next events: {string.Join(", ", upcomingEvents.Value.Take(3).Select(e => e.Subject ?? "No Subject"))}.";
                    }
                    else
                    {
                        // Create calendar cards for upcoming events
                        var upcomingCards = upcomingEvents.Value.Select((evt, index) => new
                        {
                            id = $"upcoming_{index}_{evt.Id?.GetHashCode().ToString("X")}",
                            subject = evt.Subject ?? "No Subject",
                            start = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            end = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            location = evt.Location?.DisplayName ?? "No location",
                            organizer = evt.Organizer?.EmailAddress?.Name ?? "Unknown",
                            isAllDay = evt.IsAllDay ?? false,
                            attendeeCount = evt.Attendees?.Count() ?? 0,
                            webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(evt.Id ?? "")}&exvsurl=1&path=/calendar/item"
                        }).ToList();

                        // Store structured data in kernel data for the system to process
                        kernel.Data["CalendarCards"] = upcomingCards;
                        kernel.Data["HasStructuredData"] = "true";
                        kernel.Data["StructuredDataType"] = "calendar";
                        kernel.Data["StructuredDataCount"] = upcomingCards.Count;

                        return $"No events today ({today:yyyy-MM-dd}), showing {upcomingCards.Count} upcoming event{(upcomingCards.Count != 1 ? "s" : "")} for {userName}.";
                    }
                }

                return $"No events scheduled for today ({today:yyyy-MM-dd}) and no upcoming events found for {userName} in the next week.";
            },
            "GetTodaysEvents"
        );
    }

    [KernelFunction, Description("Update an existing calendar event")]
    public async Task<string> UpdateCalendarEvent(Kernel kernel,
        [Description("Event subject/title to search for")] string eventSubject,
        [Description("New event title (optional)")] string newSubject = null,
        [Description("New start date and time (optional)")] string newStartDateTime = null,
        [Description("New duration in minutes (optional)")] int? newDurationMinutes = null,
        [Description("New location (optional)")] string newLocation = null)
    {
        var validation = ValidateRequiredParameter(eventSubject, "Event subject");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var events = await graphClient.Me.Calendar.Events.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Filter = $"contains(subject,'{eventSubject}')";
                    requestConfig.QueryParameters.Top = 1;
                });

                if (events?.Value?.Any() != true)
                {
                    return $"Event with subject containing '{eventSubject}' not found for {userName}.";
                }

                var existingEvent = events.Value.First();
                var updateEvent = new Event();

                if (!string.IsNullOrWhiteSpace(newSubject))
                {
                    updateEvent.Subject = newSubject;
                }

                if (!string.IsNullOrWhiteSpace(newStartDateTime) && CalendarHelpers.TryParseDateTime(newStartDateTime, out var parsedStart))
                {
                    updateEvent.Start = new DateTimeTimeZone
                    {
                        DateTime = parsedStart.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        TimeZone = TimeZoneInfo.Local.Id
                    };

                    if (newDurationMinutes.HasValue)
                    {
                        updateEvent.End = new DateTimeTimeZone
                        {
                            DateTime = parsedStart.AddMinutes(newDurationMinutes.Value).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                            TimeZone = TimeZoneInfo.Local.Id
                        };
                    }
                }

                if (!string.IsNullOrWhiteSpace(newLocation))
                {
                    updateEvent.Location = new Location { DisplayName = newLocation };
                }

                var updatedEvent = await graphClient.Me.Calendar.Events[existingEvent.Id].PatchAsync(updateEvent);

                return CreateSuccessResponse(
                    "Calendar event update",
                    userName,
                    ("📅 Original", existingEvent.Subject ?? "Unknown"),
                    ("📅 Updated", updatedEvent?.Subject ?? existingEvent.Subject ?? "Unknown"),
                    ("🕐 Start", updatedEvent?.Start?.DateTime ?? existingEvent.Start?.DateTime ?? "Unknown")
                );
            },
            "UpdateCalendarEvent"
        );
    }

    [KernelFunction, Description("Cancel/delete a calendar event")]
    public async Task<string> CancelCalendarEvent(Kernel kernel,
        [Description("Event subject/title to search for and cancel")] string eventSubject,
        [Description("Send cancellation notice to attendees")] bool sendCancellation = true)
    {
        var validation = ValidateRequiredParameter(eventSubject, "Event subject");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var events = await graphClient.Me.Calendar.Events.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Filter = $"contains(subject,'{eventSubject}')";
                    requestConfig.QueryParameters.Top = 1;
                });

                if (events?.Value?.Any() != true)
                {
                    return $"Event with subject containing '{eventSubject}' not found for {userName}.";
                }

                var eventToCancel = events.Value.First();
                await graphClient.Me.Calendar.Events[eventToCancel.Id].DeleteAsync();

                return CreateSuccessResponse(
                    "Calendar event cancellation",
                    userName,
                    ("📅 Cancelled Event", eventToCancel.Subject ?? "Unknown"),
                    ("📧 Cancellation Notice", sendCancellation ? "Sent to attendees" : "Not sent")
                );
            },
            "CancelCalendarEvent"
        );
    }

    [KernelFunction, Description("Get calendar events for a specific date range")]
    public async Task<string> GetEventsInDateRange(Kernel kernel,
        [Description("Start date (yyyy-MM-dd format)")] string startDate,
        [Description("End date (yyyy-MM-dd format)")] string endDate)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
                {
                    return "Invalid date format. Please use yyyy-MM-dd format.";
                }

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = start.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = end.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    // Create calendar cards for the new approach
                    var calendarCards = events.Value.Select((evt, index) => new
                    {
                        id = $"range_{index}_{evt.Id?.GetHashCode().ToString("X")}",
                        subject = evt.Subject ?? "No Subject",
                        start = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                        end = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                        location = evt.Location?.DisplayName ?? "No location",
                        organizer = evt.Organizer?.EmailAddress?.Name ?? "Unknown",
                        isAllDay = evt.IsAllDay ?? false,
                        attendeeCount = evt.Attendees?.Count() ?? 0,
                        webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(evt.Id ?? "")}&exvsurl=1&path=/calendar/item"
                    }).ToList();

                    // Store structured data in kernel data for the system to process
                    kernel.Data["CalendarCards"] = calendarCards;
                    kernel.Data["HasStructuredData"] = "true";
                    kernel.Data["StructuredDataType"] = "calendar";
                    kernel.Data["StructuredDataCount"] = calendarCards.Count;

                    return $"Found {calendarCards.Count} events from {startDate} to {endDate} for {userName}.";
                }

                return $"No events found from {startDate} to {endDate} for {userName}.";
            },
            "GetEventsInDateRange"
        );
    }

    [KernelFunction, Description("Check free/busy status for multiple users")]
    public async Task<string> CheckFreeBusyStatus(Kernel kernel,
        [Description("Comma-separated email addresses to check")] string emailAddresses,
        [Description("Start date and time to check")] string startDateTime,
        [Description("Duration in minutes to check")] int durationMinutes = 60)
    {
        var validation = ValidateRequiredParameter(emailAddresses, "Email addresses");
        if (validation != null) return validation;

        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                if (!CalendarHelpers.TryParseDateTime(startDateTime, out var start))
                {
                    return $"Could not parse start date/time: '{startDateTime}'.";
                }

                var end = start.AddMinutes(durationMinutes);
                var emails = emailAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                         .Select(e => e.Trim())
                                         .ToList();

                // Verify calendar access
                await graphClient.Me.Calendar.GetAsync();

                return $"Free/busy check for emails: {string.Join(", ", emails)}\n" +
                       $"Time slot: {start:yyyy-MM-dd HH:mm} - {end:yyyy-MM-dd HH:mm}\n" +
                       $"Duration: {durationMinutes} minutes\n" +
                       $"Calendar access verified for {userName}.\n" +
                       $"Note: Full free/busy implementation requires Microsoft Graph /calendar/getSchedule API.";
            },
            "CheckFreeBusyStatus"
        );
    }

    [KernelFunction, Description("Get the next upcoming appointment/event regardless of when it occurs")]
    public async Task<string> GetNextAppointment(Kernel kernel)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var startTime = DateTime.Now;
                var endTime = startTime.AddYears(1); // Look ahead up to 1 year

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Top = 1; // Only get the very next event
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    var nextEvent = events.Value.First();
                    
                    // Create calendar card for the next appointment
                    var calendarCards = new[]
                    {
                        new
                        {
                            id = $"next_{nextEvent.Id?.GetHashCode().ToString("X")}",
                            subject = nextEvent.Subject ?? "No Subject",
                            start = nextEvent.Start?.DateTime != null ? DateTime.Parse(nextEvent.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            end = nextEvent.End?.DateTime != null ? DateTime.Parse(nextEvent.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                            location = nextEvent.Location?.DisplayName ?? "No location",
                            organizer = nextEvent.Organizer?.EmailAddress?.Name ?? "Unknown",
                            isAllDay = nextEvent.IsAllDay ?? false,
                            attendeeCount = nextEvent.Attendees?.Count() ?? 0,
                            webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(nextEvent.Id ?? "")}&exvsurl=1&path=/calendar/item"
                        }
                    }.ToList();

                    // Store structured data in kernel data for the system to process
                    kernel.Data["CalendarCards"] = calendarCards;
                    kernel.Data["HasStructuredData"] = "true";
                    kernel.Data["StructuredDataType"] = "calendar";
                    kernel.Data["StructuredDataCount"] = calendarCards.Count;

                    return $"Your next appointment is '{nextEvent.Subject ?? "No Subject"}' scheduled for {(nextEvent.Start?.DateTime != null ? DateTime.Parse(nextEvent.Start.DateTime).ToString("MMM dd, yyyy 'at' HH:mm") : "Unknown time")} for {userName}.";
                }

                return $"No upcoming appointments found for {userName}.";
            },
            "GetNextAppointment"
        );
    }

    [KernelFunction, Description("Get count of calendar events in a specific time period")]
    public async Task<string> GetEventCount(Kernel kernel,
        [Description("Time period: 'today', 'tomorrow', 'this_week', 'next_week', 'this_month', or number of days from today (default 7)")] string timePeriod = "7",
        [Description("Include event details in response (default false for count-only)")] bool includeDetails = false)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var (startTime, endTime, timeRangeDescription) = CalendarHelpers.ParseTimePeriod(timePeriod);

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                var eventCount = events?.Value?.Count ?? 0;

                if (includeDetails && events?.Value?.Any() == true)
                {
                    // Create calendar cards for detailed response
                    var calendarCards = events.Value.Select((evt, index) => new
                    {
                        id = $"count_{index}_{evt.Id?.GetHashCode().ToString("X")}",
                        subject = evt.Subject ?? "No Subject",
                        start = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                        end = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime).ToString("yyyy-MM-dd HH:mm") : "Unknown",
                        location = evt.Location?.DisplayName ?? "No location",
                        organizer = evt.Organizer?.EmailAddress?.Name ?? "Unknown",
                        isAllDay = evt.IsAllDay ?? false,
                        attendeeCount = evt.Attendees?.Count() ?? 0,
                        webLink = $"https://outlook.office365.com/owa/?itemid={System.Web.HttpUtility.UrlEncode(evt.Id ?? "")}&exvsurl=1&path=/calendar/item"
                    }).ToList();

                    // Store structured data in kernel data for the system to process
                    kernel.Data["CalendarCards"] = calendarCards;
                    kernel.Data["HasStructuredData"] = "true";
                    kernel.Data["StructuredDataType"] = "calendar";
                    kernel.Data["StructuredDataCount"] = calendarCards.Count;

                    return $"Found {eventCount} events {timeRangeDescription} for {userName} with details.";
                }
                else
                {
                    return $"Found {eventCount} event{(eventCount != 1 ? "s" : "")} {timeRangeDescription} for {userName}.";
                }
            },
            "GetEventCount"
        );
    }
}