using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Shared;
using SharedConstants = SemanticKernelPoc.Api.Services.Shared.Constants;

namespace SemanticKernelPoc.Api.Plugins.Calendar;

public class CalendarPlugin : BaseGraphPlugin
{
    private readonly ICardBuilderService _cardBuilder;
    private readonly IAnalysisModeService _analysisMode;
    private readonly ITextProcessingService _textProcessor;

    public CalendarPlugin(
        IGraphService graphService, 
        IGraphClientFactory graphClientFactory, 
        ILogger<CalendarPlugin> logger,
        ICardBuilderService cardBuilder,
        IAnalysisModeService analysisMode,
        ITextProcessingService textProcessor) 
        : base(graphService, graphClientFactory, logger)
    {
        _cardBuilder = cardBuilder;
        _analysisMode = analysisMode;
        _textProcessor = textProcessor;
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
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.Top = Math.Min(maxEvents, SharedConstants.QueryLimits.MaxCalendarEvents);
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    if (analysisMode)
                    {
                        return await _analysisMode.GenerateAISummaryAsync(
                            kernel,
                            events.Value,
                            "upcoming calendar events",
                            userName,
                            evt => new
                            {
                                Subject = evt.Subject ?? SharedConstants.DefaultText.NoSubject,
                                Start = _textProcessor.FormatEventDateTime(evt.Start),
                                End = _textProcessor.FormatEventDateTime(evt.End),
                                Location = evt.Location?.DisplayName ?? SharedConstants.DefaultText.NoLocation,
                                Organizer = evt.Organizer?.EmailAddress?.Name ?? SharedConstants.DefaultText.Unknown,
                                IsAllDay = evt.IsAllDay ?? false,
                                AttendeeCount = evt.Attendees?.Count() ?? 0
                            });
                    }
                    else
                    {
                        var calendarCards = _cardBuilder.BuildCalendarCards(events.Value, CreateCalendarCard);
                        var functionResponse = $"Found {calendarCards.Count} upcoming events for {userName} in the next {days} days.";

                        _cardBuilder.SetCardData(kernel, "calendar", calendarCards, calendarCards.Count, functionResponse);
                        return functionResponse;
                    }
                }

                return $"No upcoming events found for {userName} in the next {days} days.";
            },
            "GetUpcomingEvents"
        );
    }

    [KernelFunction, Description("Get today's calendar events. For display purposes, use analysisMode=false. For summary/analysis requests like 'what do I have today', 'today's meetings', use analysisMode=true.")]
    public async Task<string> GetTodaysEvents(Kernel kernel,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what meetings do I have today'. Set to false for listing/displaying events. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var events = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = today.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.EndDateTime = tomorrow.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    if (analysisMode)
                    {
                        return await _analysisMode.GenerateAISummaryAsync(
                            kernel,
                            events.Value,
                            "today's calendar events",
                            userName,
                            evt => new
                            {
                                Subject = evt.Subject ?? SharedConstants.DefaultText.NoSubject,
                                Start = _textProcessor.FormatEventDateTime(evt.Start),
                                End = _textProcessor.FormatEventDateTime(evt.End),
                                Location = evt.Location?.DisplayName ?? SharedConstants.DefaultText.NoLocation,
                                IsAllDay = evt.IsAllDay ?? false
                            });
                    }
                    else
                    {
                        var calendarCards = _cardBuilder.BuildCalendarCards(events.Value, (evt, index) => CreateTodayCalendarCard(evt, index));
                        var functionResponse = $"Found {calendarCards.Count} events for today ({today:yyyy-MM-dd}) for {userName}.";

                        _cardBuilder.SetCardData(kernel, "calendar", calendarCards, calendarCards.Count, functionResponse);
                        return functionResponse;
                    }
                }

                // No events today, check for upcoming events in the next 7 days
                var weekFromNow = today.AddDays(7);
                var upcomingEvents = await graphClient.Me.Calendar.CalendarView.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.StartDateTime = today.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.EndDateTime = weekFromNow.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.Top = 3;
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (upcomingEvents?.Value?.Any() == true)
                {
                    var nextEvent = upcomingEvents.Value.First();
                    var nextEventDate = _textProcessor.FormatEventDateTime(nextEvent.Start);
                    return $"No events today for {userName}. Next upcoming event: {nextEvent.Subject} on {nextEventDate}.";
                }

                return $"No events today for {userName}. No upcoming events in the next 7 days.";
            },
            "GetTodaysEvents"
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
                var parseResult = _textProcessor.ParseEventDateTime(startDateTime);
                if (parseResult.HasError)
                {
                    return parseResult.ErrorMessage!;
                }

                var endDateTime = parseResult.DateTime!.Value.AddMinutes(durationMinutes);

                var newEvent = new Event
                {
                    Subject = subject,
                    Start = new DateTimeTimeZone
                    {
                        DateTime = parseResult.DateTime.Value.ToString(SharedConstants.DateFormats.GraphApiDateTime),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = endDateTime.ToString(SharedConstants.DateFormats.GraphApiDateTime),
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

                return CreateSuccessResponse("Calendar event created", userName,
                    ("📅 Subject", createdEvent?.Subject ?? subject),
                    ("🕐 Start", parseResult.DateTime.Value.ToString(SharedConstants.DateFormats.StandardDateTime)),
                    ("🕓 End", endDateTime.ToString(SharedConstants.DateFormats.StandardDateTime)),
                    ("⏱️ Duration", $"{durationMinutes} minutes"),
                    ("📍 Location", location ?? SharedConstants.DefaultText.NoLocation),
                    ("👥 Attendees", attendees ?? "None"));
            },
            "AddCalendarEvent"
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
                    requestConfig.QueryParameters.StartDateTime = startTime.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.EndDateTime = endTime.ToString(SharedConstants.DateFormats.GraphApiDateTime);
                    requestConfig.QueryParameters.Top = 1; // Only get the very next event
                    requestConfig.QueryParameters.Orderby = ["start/dateTime"];
                });

                if (events?.Value?.Any() == true)
                {
                    var nextEvent = events.Value.First();
                    
                    // Create a card for the next appointment instead of just text
                    var calendarCards = _cardBuilder.BuildCalendarCards([nextEvent], (evt, index) => CreateCalendarCard(evt, index));
                    var functionResponse = $"Your next appointment is on {_textProcessor.FormatEventDateTime(nextEvent.Start)} for {userName}.";

                    _cardBuilder.SetCardData(kernel, "calendar", calendarCards, 1, functionResponse);
                    return functionResponse;
                }

                return $"No upcoming appointments found for {userName}.";
            },
            "GetNextAppointment"
        );
    }

    #region Private Helper Methods

    private object CreateCalendarCard(Event evt, int index)
    {
        return new
        {
            Id = $"event_{index}_{evt.Id?.GetHashCode():X}",
            Subject = _textProcessor.TruncateText(evt.Subject, SharedConstants.TextLimits.EventSubjectMaxLength, SharedConstants.DefaultText.NoSubject),
            Start = _textProcessor.FormatEventDateTime(evt.Start, SharedConstants.DateFormats.StandardDateTime),
            End = _textProcessor.FormatEventDateTime(evt.End, SharedConstants.DateFormats.StandardDateTime),
            Location = _textProcessor.TruncateText(evt.Location?.DisplayName, SharedConstants.TextLimits.EventLocationMaxLength, SharedConstants.DefaultText.NoLocation),
            Organizer = _textProcessor.GetSafeDisplayName(evt.Organizer?.EmailAddress?.Name, evt.Organizer?.EmailAddress?.Address),
            IsAllDay = evt.IsAllDay ?? false,
            AttendeeCount = evt.Attendees?.Count() ?? 0,
            WebLink = SharedConstants.ServiceUrls.GetOutlookCalendarUrl(evt.Id ?? "")
        };
    }

    private object CreateTodayCalendarCard(Event evt, int index)
    {
        var baseCard = CreateCalendarCard(evt, index);
        var cardDict = new Dictionary<string, object>();
        
        // Copy all properties from base card
        foreach (var property in baseCard.GetType().GetProperties())
        {
            cardDict[property.Name] = property.GetValue(baseCard)!;
        }
        
        // Update specific properties for today's events
        cardDict["id"] = $"today_{index}_{evt.Id?.GetHashCode():X}";
        
        return cardDict;
    }

    #endregion
}