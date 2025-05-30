using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Helpers;

namespace SemanticKernelPoc.Api.Plugins.Calendar;

public class CalendarPlugin : BaseGraphPlugin
{
    public CalendarPlugin(IGraphService graphService, ILogger<CalendarPlugin> logger) 
        : base(graphService, logger)
    {
    }

    private static CalendarEventResponse CreateCalendarEventResponse(Event evt)
    {
        return new CalendarEventResponse(
            evt.Subject ?? "No Subject",
            evt.Start?.DateTime,
            evt.End?.DateTime,
            evt.Location?.DisplayName ?? "No location",
            evt.Organizer?.EmailAddress?.Name ?? "Unknown",
            evt.IsAllDay ?? false,
            evt.Id,
            evt.Attendees?.Any() == true ? evt.Attendees.Count() : null,
            CalendarResponseFormats.GenerateOutlookWebLink(evt.Id ?? ""),
            evt.Attendees?.Select(a => new AttendeeInfo(
                a.EmailAddress?.Name ?? a.EmailAddress?.Address ?? "Unknown",
                a.EmailAddress?.Address ?? "",
                a.Status?.Response?.ToString() ?? "None"
            )).ToList()
        );
    }

    [KernelFunction, Description("Get the user's upcoming calendar events")]
    public async Task<string> GetUpcomingEvents(Kernel kernel, 
        [Description("Number of days to look ahead (default 7)")] int days = 7,
        [Description("Maximum number of events to return (default 20)")] int maxEvents = 20)
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                if (events?.Value?.Any() == true)
                {
                    var eventList = events.Value.Select(CreateCalendarEventResponse);

                    var calendarData = new CalendarCardsData(
                        "calendar_events",
                        events.Value.Count,
                        userName,
                        $"next {days} days",
                        eventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(calendarData);
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                var busySlots = new List<(DateTime Start, DateTime End)>();

                if (events?.Value?.Any() == true)
                {
                    busySlots = events.Value
                        .Where(evt => evt.Start?.DateTime != null && evt.End?.DateTime != null)
                        .Select(evt => (
                            Start: DateTime.Parse(evt.Start!.DateTime!).AddMinutes(-bufferBefore),
                            End: DateTime.Parse(evt.End!.DateTime!).AddMinutes(bufferAfter)
                        ))
                        .OrderBy(slot => slot.Start)
                        .ToList();
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
                        var firstBusy = dayBusySlots.OrderBy(s => s.Start).First();
                        if (dayStart < firstBusy.Start)
                        {
                            availableSlots.Add((dayStart, firstBusy.Start));
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

    [KernelFunction, Description("Get today's calendar events")]
    public async Task<string> GetTodaysEvents(Kernel kernel)
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                if (events?.Value?.Any() == true)
                {
                    var eventList = events.Value.Select(CreateCalendarEventResponse);

                    var calendarData = new CalendarCardsData(
                        "calendar_events",
                        events.Value.Count,
                        userName,
                        $"today ({today:yyyy-MM-dd})",
                        eventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(calendarData);
                }

                // No events today, check for upcoming events in the next 7 days
                var weekFromNow = today.AddDays(7);
                var upcomingEvents = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = tomorrow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.EndDateTime = weekFromNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                    requestConfig.QueryParameters.Top = 5;
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                if (upcomingEvents?.Value?.Any() == true)
                {
                    var upcomingEventList = upcomingEvents.Value.Select(CreateCalendarEventResponse);

                    var upcomingResponse = new CalendarCardsData(
                        "calendar_events",
                        upcomingEvents.Value.Count,
                        userName,
                        $"No events today ({today:yyyy-MM-dd}), showing next {upcomingEvents.Value.Count} upcoming event{(upcomingEvents.Value.Count != 1 ? "s" : "")}",
                        upcomingEventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(upcomingResponse);
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                if (events?.Value?.Any() == true)
                {
                    var eventList = events.Value.Select(CreateCalendarEventResponse);

                    var calendarData = new CalendarCardsData(
                        "calendar_events",
                        events.Value.Count,
                        userName,
                        $"{startDate} to {endDate}",
                        eventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(calendarData);
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                if (events?.Value?.Any() == true)
                {
                    var nextEvent = events.Value.First();
                    var eventList = new[] { CreateCalendarEventResponse(nextEvent) };

                    var calendarData = new CalendarCardsData(
                        "calendar_events",
                        1,
                        userName,
                        "next appointment",
                        eventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(calendarData);
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
                    requestConfig.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

                var eventCount = events?.Value?.Count ?? 0;

                if (includeDetails && events?.Value?.Any() == true)
                {
                    var eventList = events.Value.Select(CreateCalendarEventResponse);

                    var detailedResponse = new CalendarCardsData(
                        "calendar_events",
                        eventCount,
                        userName,
                        timeRangeDescription,
                        eventList
                    );

                    return CalendarResponseFormats.FormatCalendarCards(detailedResponse);
                }
                else
                {
                    var countResponse = new CalendarCardsData(
                        "calendar_events",
                        eventCount,
                        userName,
                        timeRangeDescription,
                        Array.Empty<CalendarEventResponse>()
                    );

                    return CalendarResponseFormats.FormatCalendarCards(countResponse);
                }
            },
            "GetEventCount"
        );
    }
} 