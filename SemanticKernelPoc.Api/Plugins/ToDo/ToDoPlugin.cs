using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.ToDo;

public class ToDoPlugin : BaseGraphPlugin
{
    public ToDoPlugin(IGraphService graphService, ILogger<ToDoPlugin> logger) 
        : base(graphService, logger)
    {
    }

    [KernelFunction, Description("Create a new note as a To Do task")]
    public async Task<string> CreateNote(Kernel kernel,
        [Description("Note content/title")] string noteContent,
        [Description("Additional details or description (optional)")] string details = null,
        [Description("Due date for the note/reminder (optional, e.g., 'tomorrow', '2024-01-15')")] string dueDate = null,
        [Description("Priority: low, normal, or high (default normal)")] string priority = "normal",
        [Description("Task list name (optional, uses default list if not specified)")] string listName = null)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to create note - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(noteContent))
            {
                return "Note content cannot be empty. Please provide the note text.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            // Get the appropriate task list
            TodoTaskList taskList;
            try
            {
                taskList = await GetTaskList(graphClient, listName);
            }
            catch (InvalidOperationException ex)
            {
                return $"Could not find or access task list{(!string.IsNullOrEmpty(listName) ? $" '{listName}'" : "")} for {userName}: {ex.Message}";
            }

            // Create the task
            var newTask = new TodoTask
            {
                Title = noteContent.Length > 255 ? noteContent.Substring(0, 255) : noteContent,
                Body = new ItemBody
                {
                    Content = string.IsNullOrWhiteSpace(details) ? noteContent : $"{noteContent}\n\n{details}",
                    ContentType = BodyType.Text
                },
                Importance = priority.ToLower() switch
                {
                    "high" => Microsoft.Graph.Models.Importance.High,
                    "low" => Microsoft.Graph.Models.Importance.Low,
                    _ => Microsoft.Graph.Models.Importance.Normal
                }
            };

            // Set due date if provided
            if (!string.IsNullOrWhiteSpace(dueDate))
            {
                if (TryParseDate(dueDate, out var parsedDueDate))
                {
                    newTask.DueDateTime = new DateTimeTimeZone
                    {
                        DateTime = parsedDueDate.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        TimeZone = TimeZoneInfo.Local.Id
                    };
                    
                    // Log successful date parsing for debugging
                    Console.WriteLine($"Successfully parsed due date '{dueDate}' to {parsedDueDate:yyyy-MM-dd}");
                }
                else
                {
                    // Log failed date parsing for debugging
                    Console.WriteLine($"Failed to parse due date '{dueDate}', task will be created without due date");
                }
            }

            var createdTask = await graphClient.Me.Todo.Lists[taskList.Id].Tasks.PostAsync(newTask);

            // Log successful task creation for debugging
            Console.WriteLine($"Successfully created task '{createdTask?.Title}' with ID: {createdTask?.Id}");

            // Return the newly created task as a card
            var newTaskCard = new
            {
                id = createdTask?.Id,
                title = createdTask?.Title ?? noteContent,
                content = createdTask?.Body?.Content ?? details ?? noteContent,
                status = createdTask?.Status?.ToString() ?? "NotStarted",
                priority = createdTask?.Importance?.ToString() ?? priority,
                dueDate = createdTask?.DueDateTime?.DateTime,
                dueDateFormatted = createdTask?.DueDateTime?.DateTime != null ? 
                    DateTime.Parse(createdTask.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                list = taskList.DisplayName,
                created = createdTask?.CreatedDateTime?.ToString("MMM dd, yyyy") ?? DateTime.Now.ToString("MMM dd, yyyy"),
                createdDateTime = createdTask?.CreatedDateTime ?? DateTimeOffset.Now,
                isCompleted = false,
                webLink = $"https://to-do.office.com/tasks/id/{createdTask?.Id}/details",
                priorityColor = priority.ToLower() switch
                {
                    "high" => "#ef4444",
                    "low" => "#10b981", 
                    _ => "#6b7280"
                },
                statusColor = "#3b82f6", // New task color
                isNewlyCreated = true // Flag to indicate this is a newly created task
            };

            return $"NOTE_CARDS: {JsonSerializer.Serialize(new[] { newTaskCard }, new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
        {
            var errorMessage = $"Microsoft Graph API Error: {odataEx.Error?.Code} - {odataEx.Error?.Message}";
            Console.WriteLine($"Graph API error in CreateNote: {errorMessage}");
            return $"❌ Error creating note: {errorMessage}";
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            Console.WriteLine($"Unexpected error in CreateNote: {errorMessage}\nStack trace: {ex.StackTrace}");
            return $"❌ Error creating note: {errorMessage}";
        }
    }

    [KernelFunction, Description("Get recent notes (To Do tasks). Use this when user asks for 'notes', 'my notes', 'recent notes', 'show notes', etc.")]
    public async Task<string> GetRecentNotes(Kernel kernel,
        [Description("Number of recent notes to retrieve (default 10)")] int count = 10,
        [Description("Include completed notes (default false)")] bool includeCompleted = false,
        [Description("Task list name (optional, searches all lists if not specified)")] string listName = null)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to access notes - user authentication required.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            var allTasks = new List<(TodoTask Task, string ListName)>();

            if (!string.IsNullOrWhiteSpace(listName))
            {
                // Search specific list
                try
                {
                    var taskList = await GetTaskList(graphClient, listName);
                    if (!string.IsNullOrEmpty(taskList.Id))
                    {
                        var tasks = await GetTasksFromList(graphClient, taskList.Id, includeCompleted);
                        allTasks.AddRange(tasks.Select(t => (t, taskList.DisplayName ?? "Unknown")));
                    }
                }
                catch (InvalidOperationException)
                {
                    // List not found, continue with empty results
                }
            }
            else
            {
                // Search all lists
                var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
                if (taskLists?.Value?.Any() == true)
                {
                    foreach (var list in taskLists.Value)
                    {
                        if (list.Id != null)
                        {
                            var tasks = await GetTasksFromList(graphClient, list.Id, includeCompleted);
                            allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? "Unknown")));
                        }
                    }
                }
            }

            if (!allTasks.Any())
            {
                return $"No {(includeCompleted ? "" : "incomplete ")}notes found for {userName}.";
            }

            // Sort by creation time (most recent first) and take the requested count
            var recentTasks = allTasks
                .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTimeOffset.MinValue)
                .Take(Math.Min(count, 20))
                .ToList();

                        // Create note cards similar to calendar cards
            var noteCards = recentTasks.Select(t => new
                {
                id = t.Task.Id,
                title = t.Task.Title ?? "Untitled Note",
                content = t.Task.Body?.Content?.Length > 150 ? 
                    t.Task.Body.Content.Substring(0, 150) + "..." : 
                    t.Task.Body?.Content ?? "",
                status = t.Task.Status?.ToString() ?? "NotStarted",
                priority = t.Task.Importance?.ToString() ?? "Normal",
                dueDate = t.Task.DueDateTime?.DateTime,
                dueDateFormatted = t.Task.DueDateTime?.DateTime != null ? 
                    DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                list = t.ListName,
                created = t.Task.CreatedDateTime?.ToString("MMM dd, yyyy") ?? "Unknown",
                createdDateTime = t.Task.CreatedDateTime,
                isCompleted = t.Task.Status?.ToString()?.ToLower() == "completed",
                webLink = $"https://to-do.office.com/tasks/{t.Task.Id}/details",
                priorityColor = t.Task.Importance?.ToString()?.ToLower() switch
                {
                    "high" => "#ef4444",
                    "low" => "#10b981", 
                    _ => "#6b7280"
                },
                statusColor = t.Task.Status?.ToString()?.ToLower() switch
                {
                    "completed" => "#10b981",
                    "inprogress" => "#f59e0b",
                    "waitingonothers" => "#8b5cf6",
                    "deferred" => "#6b7280",
                    _ => "#3b82f6"
                }
            }).ToList();

            return $"NOTE_CARDS: {JsonSerializer.Serialize(noteCards, new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Exception ex)
        {
            return $"❌ Error getting recent notes: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search notes (To Do tasks) by content")]
    public async Task<string> SearchNotes(Kernel kernel,
        [Description("Search query to find in note titles and content")] string searchQuery,
        [Description("Maximum number of results (default 10)")] int maxResults = 10,
        [Description("Include completed notes in search (default false)")] bool includeCompleted = false)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to search notes - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return "Please provide a search query to find notes.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            var allTasks = new List<(TodoTask Task, string ListName)>();

            // Search all lists
            var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
            if (taskLists?.Value?.Any() == true)
            {
                foreach (var list in taskLists.Value)
                {
                    if (list.Id != null)
                    {
                        var tasks = await GetTasksFromList(graphClient, list.Id, includeCompleted);
                        allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? "Unknown")));
                    }
                }
            }

            // Filter tasks by search query (case-insensitive)
            var searchLower = searchQuery.ToLower();
            var matchingTasks = allTasks
                .Where(t => 
                    (t.Task.Title?.ToLower().Contains(searchLower) == true) ||
                    (t.Task.Body?.Content?.ToLower().Contains(searchLower) == true))
                .Take(Math.Min(maxResults, 20))
                .ToList();

            if (!matchingTasks.Any())
            {
                return $"No notes found matching '{searchQuery}' for {userName}.";
            }

            // Create note cards for search results
            var noteCards = matchingTasks.Select(t => new
            {
                id = t.Task.Id,
                title = t.Task.Title ?? "Untitled Note",
                content = t.Task.Body?.Content?.Length > 150 ? 
                    t.Task.Body.Content.Substring(0, 150) + "..." : 
                    t.Task.Body?.Content ?? "",
                status = t.Task.Status?.ToString() ?? "NotStarted",
                priority = t.Task.Importance?.ToString() ?? "Normal",
                dueDate = t.Task.DueDateTime?.DateTime,
                dueDateFormatted = t.Task.DueDateTime?.DateTime != null ? 
                    DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                list = t.ListName,
                created = t.Task.CreatedDateTime?.ToString("MMM dd, yyyy") ?? "Unknown",
                createdDateTime = t.Task.CreatedDateTime,
                isCompleted = t.Task.Status?.ToString()?.ToLower() == "completed",
                matchReason = (t.Task.Title?.ToLower().Contains(searchLower) == true) ? "Title" : "Content",
                webLink = $"https://to-do.office.com/tasks/id/{t.Task.Id}/details",
                priorityColor = t.Task.Importance?.ToString()?.ToLower() switch
                {
                    "high" => "#ef4444",
                    "low" => "#10b981", 
                    _ => "#6b7280"
                },
                statusColor = t.Task.Status?.ToString()?.ToLower() switch
                {
                    "completed" => "#10b981",
                    "inprogress" => "#f59e0b",
                    "waitingonothers" => "#8b5cf6",
                    "deferred" => "#6b7280",
                    _ => "#3b82f6"
                }
            }).ToList();

            return $"NOTE_CARDS: {JsonSerializer.Serialize(noteCards, new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Exception ex)
        {
            return $"❌ Error searching notes: {ex.Message}";
        }
    }

    [KernelFunction, Description("Mark a note as complete or update its status")]
    public async Task<string> UpdateNoteStatus(Kernel kernel,
        [Description("Note title or part of title to find")] string noteTitle,
        [Description("New status: notstarted, inprogress, completed, waitingonothers, or deferred")] string status = "completed")
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to update note - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(noteTitle))
            {
                return "Please provide the note title to update.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            // Find the task
            try
            {
                var foundTask = await FindTaskByTitle(graphClient, noteTitle);
                
                // Update the task status
                var updateTask = new TodoTask
                {
                    Status = status.ToLower() switch
                    {
                        "notstarted" => Microsoft.Graph.Models.TaskStatus.NotStarted,
                        "inprogress" => Microsoft.Graph.Models.TaskStatus.InProgress,
                        "completed" => Microsoft.Graph.Models.TaskStatus.Completed,
                        "waitingonothers" => Microsoft.Graph.Models.TaskStatus.WaitingOnOthers,
                        "deferred" => Microsoft.Graph.Models.TaskStatus.Deferred,
                        _ => Microsoft.Graph.Models.TaskStatus.Completed
                    }
                };

                await graphClient.Me.Todo.Lists[foundTask.ListId].Tasks[foundTask.Task.Id].PatchAsync(updateTask);

                return $"✅ Note updated successfully for {userName}!\n" +
                       $"📝 Note: {foundTask.Task.Title}\n" +
                       $"📋 List: {foundTask.ListName}\n" +
                       $"🔄 Status: {status}\n" +
                       (status.ToLower() == "completed" ? "🎉 Note marked as complete!" : "");
            }
            catch (InvalidOperationException ex)
            {
                return $"No note found with title containing '{noteTitle}' for {userName}: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error updating note: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get task lists (note categories)")]
    public async Task<string> GetTaskLists(Kernel kernel)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to access task lists - user authentication required.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            var taskLists = await graphClient.Me.Todo.Lists.GetAsync();

            if (taskLists?.Value?.Any() == true)
            {
                var lists = taskLists.Value.Select(list => new
                {
                    Name = list.DisplayName,
                    IsOwner = list.IsOwner ?? false,
                    IsShared = list.IsShared ?? false,
                    WellknownListName = list.WellknownListName?.ToString()
                });

                return $"Task lists (note categories) for {userName}:\n" +
                       JsonSerializer.Serialize(lists, new JsonSerializerOptions { WriteIndented = true });
            }

            return $"No task lists found for {userName}.";
        }
        catch (Exception ex)
        {
            return $"❌ Error getting task lists: {ex.Message}";
        }
    }

    private async Task<TodoTaskList> GetTaskList(GraphServiceClient graphClient, string listName)
    {
        var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
        
        if (taskLists?.Value?.Any() != true)
            throw new InvalidOperationException("No task lists found for user");

        if (string.IsNullOrWhiteSpace(listName))
        {
            // Return default list (usually "Tasks")
            return taskLists.Value.FirstOrDefault(l => l.WellknownListName == WellknownListName.DefaultList) 
                ?? taskLists.Value.First();
        }

        // Find list by name (case-insensitive)
        var foundList = taskLists.Value.FirstOrDefault(l => 
            l.DisplayName?.Equals(listName, StringComparison.OrdinalIgnoreCase) == true);
        
        if (foundList == null)
            throw new InvalidOperationException($"Task list '{listName}' not found");
            
        return foundList;
    }

    private async Task<List<TodoTask>> GetTasksFromList(GraphServiceClient graphClient, string listId, bool includeCompleted)
    {
        var tasks = await graphClient.Me.Todo.Lists[listId].Tasks.GetAsync(requestConfig =>
        {
            if (!includeCompleted)
            {
                requestConfig.QueryParameters.Filter = "status ne 'completed'";
            }
            requestConfig.QueryParameters.Top = 50;
            requestConfig.QueryParameters.Orderby = new[] { "createdDateTime desc" };
        });

        return tasks?.Value?.ToList() ?? new List<TodoTask>();
    }

    private async Task<(TodoTask Task, string ListId, string ListName)> FindTaskByTitle(GraphServiceClient graphClient, string titleSearch)
    {
        var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
        
        if (taskLists?.Value?.Any() != true)
            throw new InvalidOperationException("No task lists found for user");

        var searchLower = titleSearch.ToLower();

        foreach (var list in taskLists.Value)
        {
            if (string.IsNullOrEmpty(list.Id)) continue;

            var tasks = await GetTasksFromList(graphClient, list.Id, true); // Include completed
            var matchingTask = tasks.FirstOrDefault(t => 
                t.Title?.ToLower().Contains(searchLower) == true);

            if (matchingTask != null)
            {
                return (matchingTask, list.Id, list.DisplayName ?? "Unknown");
            }
        }

        throw new InvalidOperationException($"No task found with title containing '{titleSearch}'");
    }

    private bool TryParseDate(string input, out DateTime result)
    {
        result = default;

        // Handle natural language
        var normalized = input.ToLower().Trim();
        if (normalized == "today")
        {
            result = DateTime.Today;
            return true;
        }
        if (normalized == "tomorrow")
        {
            result = DateTime.Today.AddDays(1);
            return true;
        }

        // Handle month names with day (e.g., "june 2", "december 25")
        if (TryParseMonthDay(normalized, out result))
        {
            return true;
        }

        // Try standard formats
        var formats = new[]
        {
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "yyyy/MM/dd",
            "MM-dd-yyyy",
            "dd-MM-yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out result))
            {
                return true;
            }
        }

        // Try general parsing (this should handle "june 2", "June 2nd", etc.)
        return DateTime.TryParse(input, out result);
    }

    private bool TryParseMonthDay(string input, out DateTime result)
    {
        result = default;
        
        try
        {
            // Handle formats like "june 2", "december 25", "jan 15"
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var monthPart = parts[0];
                var dayPart = parts[1];

                // Try to parse the day
                if (int.TryParse(dayPart, out var day))
                {
                    var currentYear = DateTime.Now.Year;
                    
                    // Try to parse month name
                    var monthNames = new[]
                    {
                        "january", "february", "march", "april", "may", "june",
                        "july", "august", "september", "october", "november", "december",
                        "jan", "feb", "mar", "apr", "may", "jun",
                        "jul", "aug", "sep", "oct", "nov", "dec"
                    };

                    var monthIndex = Array.FindIndex(monthNames, m => m == monthPart);
                    if (monthIndex >= 0)
                    {
                        var month = (monthIndex % 12) + 1; // Convert to 1-12
                        
                        // Create the date for this year
                        var targetDate = new DateTime(currentYear, month, day);
                        
                        // If the date has already passed this year, use next year
                        if (targetDate < DateTime.Today)
                        {
                            targetDate = new DateTime(currentYear + 1, month, day);
                        }
                        
                        result = targetDate;
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Fall through to return false
        }
        
        return false;
    }

    private GraphServiceClient CreateGraphClient(string userAccessToken)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userAccessToken);
        
        return new GraphServiceClient(httpClient);
    }
} 