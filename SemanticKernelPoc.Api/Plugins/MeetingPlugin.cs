using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text.Json;

namespace SemanticKernelPoc.Api.Plugins;

public class MeetingPlugin
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly Kernel _kernel;

    public MeetingPlugin(GraphServiceClient graphServiceClient, Kernel kernel)
    {
        _graphServiceClient = graphServiceClient;
        _kernel = kernel;
    }

    [KernelFunction]
    [Description("Get recent meeting transcripts from Microsoft Teams meetings")]
    public async Task<string> GetMeetingTranscripts(
        [Description("Number of recent meetings to retrieve (default: 10)")] int count = 10,
        [Description("Number of days to look back (default: 30)")] int daysBack = 30)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddDays(-daysBack);
            
            // Get calendar events that are Teams meetings
            var events = await _graphServiceClient.Me.Events
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = 
                        $"start/dateTime ge '{startTime:yyyy-MM-ddTHH:mm:ss.fffZ}' and " +
                        "isOnlineMeeting eq true";
                    requestConfiguration.QueryParameters.Top = count;
                    requestConfiguration.QueryParameters.Orderby = new[] { "start/dateTime desc" };
                    requestConfiguration.QueryParameters.Select = new[] { 
                        "id", "subject", "start", "end", "onlineMeeting", "attendees" 
                    };
                });

            if (events?.Value == null || !events.Value.Any())
            {
                return "MEETING_TRANSCRIPTS: No recent Teams meetings found in the specified time range.";
            }

            var meetingsWithTranscripts = new List<object>();

            foreach (var meeting in events.Value)
            {
                try
                {
                    // Check if this meeting has an online meeting ID
                    if (meeting.OnlineMeeting?.JoinUrl == null)
                        continue;

                    // Extract the online meeting ID from the join URL or use the calendar event ID
                    var onlineMeetingId = ExtractOnlineMeetingId(meeting);
                    
                    if (string.IsNullOrEmpty(onlineMeetingId))
                        continue;

                    // Check for transcripts for this online meeting
                    var transcripts = await _graphServiceClient.Me.OnlineMeetings[onlineMeetingId].Transcripts.GetAsync();
                    
                    if (transcripts?.Value?.Any() == true)
                    {
                        var latestTranscript = transcripts.Value.OrderByDescending(t => t.CreatedDateTime).FirstOrDefault();
                        
                        // Get a preview of the transcript content
                        var transcriptPreview = await GetTranscriptPreview(onlineMeetingId, latestTranscript?.Id);
                        
                        meetingsWithTranscripts.Add(new
                        {
                            Id = onlineMeetingId,
                            CalendarEventId = meeting.Id,
                            Subject = meeting.Subject ?? "Untitled Meeting",
                            StartTime = meeting.Start?.DateTime,
                            EndTime = meeting.End?.DateTime,
                            AttendeeCount = meeting.Attendees?.Count() ?? 0,
                            TranscriptCount = transcripts.Value.Count(),
                            LatestTranscriptId = latestTranscript?.Id,
                            TranscriptCreated = latestTranscript?.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                            HasTranscript = true,
                            TranscriptPreview = transcriptPreview
                        });
                    }
                }
                catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
                {
                    // Log specific Graph API errors but continue processing
                    if (odataEx.Error?.Code != "NotFound") // Ignore not found errors for meetings without transcripts
                    {
                        Console.WriteLine($"Graph API error for meeting {meeting.Id}: {odataEx.Error?.Code} - {odataEx.Error?.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Continue with other meetings if one fails
                    Console.WriteLine($"Failed to check transcript for meeting {meeting.Id}: {ex.Message}");
                }
            }

            if (!meetingsWithTranscripts.Any())
            {
                return "MEETING_TRANSCRIPTS: Recent Teams meetings found, but no transcripts are available yet. " +
                       "Transcripts are only available for meetings where recording/transcription was enabled and " +
                       "may take some time to be processed after meetings end.";
            }

            var result = new
            {
                Type = "meeting_transcripts",
                Count = meetingsWithTranscripts.Count,
                TimeRange = $"Last {daysBack} days",
                TotalMeetingsChecked = events.Value.Count(),
                Meetings = meetingsWithTranscripts
            };

            return $"MEETING_TRANSCRIPTS: {JsonSerializer.Serialize(result)}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving meeting transcripts: {ex.Message}";
        }
    }

    private string ExtractOnlineMeetingId(Event calendarEvent)
    {
        // For now, we'll use the calendar event ID as the online meeting ID
        // In a real implementation, you might need to extract this from the join URL
        // or use a different approach based on how your organization's meetings are structured
        return calendarEvent.Id;
    }

    private async Task<string> GetTranscriptPreview(string onlineMeetingId, string transcriptId)
    {
        try
        {
            if (string.IsNullOrEmpty(transcriptId))
                return "No transcript content available";

            var transcriptContent = await _graphServiceClient.Me.OnlineMeetings[onlineMeetingId]
                .Transcripts[transcriptId].Content.GetAsync();

            if (transcriptContent == null)
                return "Transcript content not accessible";

            using var reader = new StreamReader(transcriptContent);
            var content = await reader.ReadToEndAsync();
            var parsedContent = ParseVttContent(content);
            
            if (string.IsNullOrEmpty(parsedContent))
                return "Transcript processing in progress...";

            return parsedContent.Length > 150 ? 
                parsedContent.Substring(0, 150) + "..." : parsedContent;
        }
        catch
        {
            return "Transcript preview not available";
        }
    }

    [KernelFunction]
    [Description("Get full transcript content for a specific meeting")]
    public async Task<string> GetMeetingTranscript(
        [Description("The meeting ID to get transcript for")] string meetingId)
    {
        try
        {
            // First, try to get the online meeting details
            var onlineMeeting = await _graphServiceClient.Me.OnlineMeetings[meetingId].GetAsync();
            
            if (onlineMeeting == null)
            {
                return "Meeting not found or not accessible.";
            }

            // Get all transcripts for this meeting
            var transcripts = await _graphServiceClient.Me.OnlineMeetings[meetingId].Transcripts.GetAsync();
            
            if (transcripts?.Value == null || !transcripts.Value.Any())
            {
                return "No transcripts available for this meeting. Transcripts are only available for meetings where recording/transcription was enabled and may take time to be processed after meetings end.";
            }

            // Get the most recent transcript
            var latestTranscript = transcripts.Value.OrderByDescending(t => t.CreatedDateTime).FirstOrDefault();
            
            if (latestTranscript?.Id == null)
            {
                return "No valid transcript found for this meeting.";
            }

            // Get the full transcript content
            var transcriptContent = await _graphServiceClient.Me.OnlineMeetings[meetingId]
                .Transcripts[latestTranscript.Id].Content.GetAsync();

            if (transcriptContent == null)
            {
                return "Transcript content is not accessible at this time.";
            }

            // Convert the stream to string and parse
            using var reader = new StreamReader(transcriptContent);
            var content = await reader.ReadToEndAsync();
            var parsedTranscript = ParseVttContent(content);

            if (string.IsNullOrEmpty(parsedTranscript))
            {
                return "Transcript content could not be processed.";
            }

            var result = new
            {
                Type = "meeting_transcript",
                MeetingId = meetingId,
                TranscriptId = latestTranscript.Id,
                Subject = onlineMeeting.Subject ?? "Untitled Meeting",
                TranscriptCreated = latestTranscript.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                TotalTranscripts = transcripts.Value.Count(),
                Transcript = parsedTranscript,
                WordCount = parsedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            };

            return $"MEETING_TRANSCRIPT: {JsonSerializer.Serialize(result)}";
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
        {
            if (odataEx.Error?.Code == "Forbidden" || odataEx.Error?.Code == "Unauthorized")
            {
                return "Access denied: Insufficient permissions to access meeting transcripts. " +
                       "Required permissions: OnlineMeetingTranscript.Read.All or OnlineMeetingTranscript.Read.Chat";
            }
            if (odataEx.Error?.Code == "NotFound")
            {
                return "Meeting or transcript not found.";
            }
            return $"Graph API Error: {odataEx.Error?.Code} - {odataEx.Error?.Message}";
        }
        catch (Exception ex)
        {
            return $"Error retrieving meeting transcript: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Generate an AI summary of a meeting transcript")]
    public async Task<string> SummarizeMeeting(
        [Description("The meeting ID to summarize")] string meetingId)
    {
        try
        {
            var transcript = await GetMeetingTranscriptContent(meetingId);
            
            if (string.IsNullOrEmpty(transcript))
            {
                return "Cannot summarize meeting: No transcript available.";
            }

            var summaryPrompt = $@"
Please provide a comprehensive summary of this meeting transcript:

TRANSCRIPT:
{transcript}

Please structure your summary as follows:
1. **Meeting Overview**: Brief description of the meeting purpose and main topics
2. **Key Discussion Points**: Main topics discussed with bullet points
3. **Participants**: Who spoke and their main contributions
4. **Duration and Flow**: How the meeting progressed

Keep the summary concise but informative, focusing on the most important content.
";

            var summary = await _kernel.InvokePromptAsync(summaryPrompt);
            return summary.ToString();
        }
        catch (Exception ex)
        {
            return $"Error summarizing meeting: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Extract key decisions made during a meeting")]
    public async Task<string> ExtractKeyDecisions(
        [Description("The meeting ID to extract decisions from")] string meetingId)
    {
        try
        {
            var transcript = await GetMeetingTranscriptContent(meetingId);
            
            if (string.IsNullOrEmpty(transcript))
            {
                return "Cannot extract decisions: No transcript available.";
            }

            var decisionsPrompt = $@"
Analyze this meeting transcript and extract all key decisions that were made:

TRANSCRIPT:
{transcript}

Please identify and list:
1. **Decisions Made**: Clear decisions that were agreed upon
2. **Action Items**: Specific tasks or actions assigned to people
3. **Next Steps**: What needs to happen next
4. **Deadlines**: Any dates or timelines mentioned

Format each decision clearly with:
- What was decided
- Who is responsible (if mentioned)
- When it needs to be done (if mentioned)

If no clear decisions were made, state that explicitly.
";

            var decisions = await _kernel.InvokePromptAsync(decisionsPrompt);
            return decisions.ToString();
        }
        catch (Exception ex)
        {
            return $"Error extracting decisions: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Propose tasks based on meeting transcript content")]
    public async Task<string> ProposeTasksFromMeeting(
        [Description("The meeting ID to generate tasks from")] string meetingId)
    {
        try
        {
            var transcript = await GetMeetingTranscriptContent(meetingId);
            
            if (string.IsNullOrEmpty(transcript))
            {
                return "Cannot propose tasks: No transcript available.";
            }

            var tasksPrompt = $@"
Based on this meeting transcript, propose specific, actionable tasks that should be created:

TRANSCRIPT:
{transcript}

For each proposed task, provide:
1. **Title**: Clear, concise task title
2. **Description**: Detailed description of what needs to be done
3. **Priority**: High, Medium, or Low
4. **Suggested Due Date**: When this should be completed (relative to today)
5. **Assigned To**: Who should do this (if mentioned in the meeting)

Format your response as a JSON array of task objects like this:
[
  {{
    ""title"": ""Task title"",
    ""description"": ""Detailed description"",
    ""priority"": ""High"",
    ""suggestedDueDate"": ""2024-01-15"",
    ""assignedTo"": ""Person name or 'Unassigned'""
  }}
]

Only propose tasks that are clearly actionable and mentioned in the meeting. If no clear tasks can be identified, return an empty array.
";

            var tasksResponse = await _kernel.InvokePromptAsync(tasksPrompt);
            var tasksJson = tasksResponse.ToString();

            // Try to parse and validate the JSON
            try
            {
                var tasks = JsonSerializer.Deserialize<object[]>(tasksJson);
                return $"TASK_PROPOSALS: {tasksJson}";
            }
            catch (JsonException)
            {
                // If JSON parsing fails, return a formatted response
                return $"TASK_PROPOSALS_TEXT: {tasksJson}";
            }
        }
        catch (Exception ex)
        {
            return $"Error proposing tasks: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Create Microsoft To Do tasks from a list of task proposals")]
    public async Task<string> CreateTasksFromProposals(
        [Description("JSON array of task proposals to create")] string taskProposalsJson)
    {
        try
        {
            var taskProposals = JsonSerializer.Deserialize<TaskProposal[]>(taskProposalsJson);
            
            if (taskProposals == null || !taskProposals.Any())
            {
                return "No valid task proposals provided.";
            }

            var createdTasks = new List<object>();

            foreach (var proposal in taskProposals)
            {
                try
                {
                    var todoTask = new TodoTask
                    {
                        Title = proposal.Title,
                        Body = new ItemBody
                        {
                            Content = proposal.Description,
                            ContentType = BodyType.Text
                        },
                        Importance = proposal.Priority?.ToLower() switch
                        {
                            "high" => Importance.High,
                            "low" => Importance.Low,
                            _ => Importance.Normal
                        }
                    };

                    // Set due date if provided
                    if (DateTime.TryParse(proposal.SuggestedDueDate, out var dueDate))
                    {
                        todoTask.DueDateTime = new DateTimeTimeZone
                        {
                            DateTime = dueDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                            TimeZone = TimeZoneInfo.Local.Id
                        };
                    }

                    // Get the default task list first
                    var taskLists = await _graphServiceClient.Me.Todo.Lists.GetAsync();
                    var defaultList = taskLists?.Value?.FirstOrDefault(l => l.WellknownListName == Microsoft.Graph.Models.WellknownListName.DefaultList) 
                                     ?? taskLists?.Value?.FirstOrDefault();
                    
                    if (defaultList?.Id == null)
                    {
                        throw new InvalidOperationException("No task list available for creating tasks");
                    }

                    var createdTask = await _graphServiceClient.Me.Todo.Lists[defaultList.Id].Tasks
                        .PostAsync(todoTask);

                    createdTasks.Add(new
                    {
                        Id = createdTask?.Id,
                        Title = createdTask?.Title,
                        Status = "Created",
                        OriginalProposal = proposal
                    });
                }
                catch (Exception ex)
                {
                    createdTasks.Add(new
                    {
                        Title = proposal.Title,
                        Status = "Failed",
                        Error = ex.Message,
                        OriginalProposal = proposal
                    });
                }
            }

            // Return successfully created tasks as NOTE_CARDS
            var successfulTasks = createdTasks
                .Where(t => t.GetType().GetProperty("Status")?.GetValue(t)?.ToString() == "Created")
                .ToList();

            if (successfulTasks.Any())
            {
                var noteCards = successfulTasks.Select(t => new
                {
                    id = t.GetType().GetProperty("Id")?.GetValue(t)?.ToString(),
                    title = t.GetType().GetProperty("Title")?.GetValue(t)?.ToString(),
                    content = ((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.Description ?? "",
                    status = "NotStarted",
                    priority = ((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.Priority ?? "Normal",
                    dueDate = ((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.SuggestedDueDate,
                    dueDateFormatted = !string.IsNullOrEmpty(((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.SuggestedDueDate) && 
                                      DateTime.TryParse(((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.SuggestedDueDate, out var dueDate) ? 
                                      dueDate.ToString("MMM dd, yyyy") : null,
                    list = "Tasks",
                    created = DateTime.Now.ToString("MMM dd, yyyy"),
                    createdDateTime = DateTimeOffset.Now,
                    isCompleted = false,
                    webLink = $"https://to-do.office.com/tasks/id/{t.GetType().GetProperty("Id")?.GetValue(t)?.ToString()}/details",
                    priorityColor = ((TaskProposal)t.GetType().GetProperty("OriginalProposal")?.GetValue(t))?.Priority?.ToLower() switch
                    {
                        "high" => "#ef4444",
                        "low" => "#10b981", 
                        _ => "#6b7280"
                    },
                    statusColor = "#3b82f6", // New task color
                    isNewlyCreated = true // Flag to indicate these are newly created tasks
                }).ToList();

                return $"NOTE_CARDS: {JsonSerializer.Serialize(noteCards, new JsonSerializerOptions { WriteIndented = false })}";
            }
            else
            {
                var failedCount = createdTasks.Count(t => t.GetType().GetProperty("Status")?.GetValue(t)?.ToString() == "Failed");
                return $"❌ Failed to create {failedCount} task{(failedCount != 1 ? "s" : "")}. Please check the task details and try again.";
            }
        }
        catch (Exception ex)
        {
            return $"Error creating tasks: {ex.Message}";
        }
    }

    private async Task<string> GetMeetingTranscriptContent(string meetingId)
    {
        try
        {
            // First, try to get the online meeting to verify it exists and get the organizer
            var onlineMeeting = await _graphServiceClient.Me.OnlineMeetings[meetingId].GetAsync();
            
            if (onlineMeeting == null)
            {
                return null;
            }

            // Get all transcripts for this meeting
            var transcripts = await _graphServiceClient.Me.OnlineMeetings[meetingId].Transcripts.GetAsync();
            
            if (transcripts?.Value == null || !transcripts.Value.Any())
            {
                return null;
            }

            // Get the most recent transcript (they are usually ordered by creation time)
            var latestTranscript = transcripts.Value.OrderByDescending(t => t.CreatedDateTime).FirstOrDefault();
            
            if (latestTranscript?.Id == null)
            {
                return null;
            }

            // Get the transcript content
            var transcriptContent = await _graphServiceClient.Me.OnlineMeetings[meetingId]
                .Transcripts[latestTranscript.Id].Content.GetAsync();

            if (transcriptContent == null)
            {
                return null;
            }

            // Convert the stream to string
            using var reader = new StreamReader(transcriptContent);
            var content = await reader.ReadToEndAsync();
            
            // Parse VTT format to extract just the text content
            return ParseVttContent(content);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
        {
            // Handle specific Graph API errors
            if (odataEx.Error?.Code == "Forbidden" || odataEx.Error?.Code == "Unauthorized")
            {
                return "Access denied: Insufficient permissions to access meeting transcripts. " +
                       "Required permissions: OnlineMeetingTranscript.Read.All or OnlineMeetingTranscript.Read.Chat";
            }
            if (odataEx.Error?.Code == "NotFound")
            {
                return null; // Meeting or transcript not found
            }
            return $"Graph API Error: {odataEx.Error?.Code} - {odataEx.Error?.Message}";
        }
        catch (Exception ex)
        {
            // Log the error but don't expose internal details
            return $"Error retrieving transcript: {ex.Message}";
        }
    }

    private string ParseVttContent(string vttContent)
    {
        if (string.IsNullOrEmpty(vttContent))
            return null;

        var lines = vttContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var transcriptText = new List<string>();
        var currentSpeaker = "";
        var currentText = "";

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip VTT headers and timing lines
            if (trimmedLine.StartsWith("WEBVTT") || 
                trimmedLine.Contains("-->") || 
                string.IsNullOrEmpty(trimmedLine))
                continue;

            // Parse speaker and text from VTT format: <v Speaker Name>Text</v>
            if (trimmedLine.StartsWith("<v ") && trimmedLine.Contains(">") && trimmedLine.EndsWith("</v>"))
            {
                var speakerStart = trimmedLine.IndexOf("<v ") + 3;
                var speakerEnd = trimmedLine.IndexOf(">", speakerStart);
                var textStart = speakerEnd + 1;
                var textEnd = trimmedLine.LastIndexOf("</v>");

                if (speakerEnd > speakerStart && textEnd > textStart)
                {
                    var speaker = trimmedLine.Substring(speakerStart, speakerEnd - speakerStart);
                    var text = trimmedLine.Substring(textStart, textEnd - textStart);

                    if (speaker != currentSpeaker)
                    {
                        if (!string.IsNullOrEmpty(currentText))
                        {
                            transcriptText.Add($"{currentSpeaker}: {currentText}");
                        }
                        currentSpeaker = speaker;
                        currentText = text;
                    }
                    else
                    {
                        currentText += " " + text;
                    }
                }
            }
            else if (!trimmedLine.StartsWith("<") && !string.IsNullOrEmpty(currentSpeaker))
            {
                // Handle continuation lines
                currentText += " " + trimmedLine;
            }
        }

        // Add the last speaker's text
        if (!string.IsNullOrEmpty(currentText))
        {
            transcriptText.Add($"{currentSpeaker}: {currentText}");
        }

        return transcriptText.Any() ? string.Join("\n\n", transcriptText) : vttContent;
    }

    public class TaskProposal
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public string SuggestedDueDate { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = "Unassigned";
    }
} 