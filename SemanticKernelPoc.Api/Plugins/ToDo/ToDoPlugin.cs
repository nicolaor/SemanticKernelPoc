using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.ToDo;

public class ToDoPlugin : BaseGraphPlugin
{
    private readonly IGraphService _graphService;

    public ToDoPlugin(IGraphService graphService, ILogger<ToDoPlugin> logger) 
        : base(graphService, logger)
    {
        _graphService = graphService;
    }

    [KernelFunction, Description("Create a new task as a To Do task")]
    public async Task<string> CreateNote(Kernel kernel,
        [Description("Task content/title")] string noteContent,
        [Description("Additional details or description (optional)")] string details = null,
        [Description("Due date for the task/reminder (optional, e.g., 'tomorrow', '2024-01-15')")] string dueDate = null,
        [Description("Priority: low, normal, or high (default normal)")] string priority = "normal",
        [Description("Task list name (optional, uses default list if not specified)")] string listName = null)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to create task - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(noteContent))
            {
                return "Please provide task content to create a task.";
            }

            var graphClient = CreateGraphClient(userAccessToken);

            // Find or use default task list
            TodoTaskList targetList;
            if (!string.IsNullOrWhiteSpace(listName))
            {
                try
                {
                    targetList = await GetTaskList(graphClient, listName);
                }
                catch (InvalidOperationException)
                {
                    return $"Task list '{listName}' not found for {userName}. Please check the list name or create the list first.";
                }
            }
            else
            {
                // Use the default task list (usually "Tasks")
                var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
                targetList = taskLists?.Value?.FirstOrDefault(l => l.WellknownListName == WellknownListName.DefaultList) ??
                           taskLists?.Value?.FirstOrDefault();
                
                if (targetList == null)
                {
                    return $"No task lists found for {userName}. Please create a task list first.";
                }
            }

            // Parse due date if provided
            DateTime? parsedDueDate = null;
            if (!string.IsNullOrWhiteSpace(dueDate))
            {
                if (TryParseDate(dueDate, out var result))
                {
                    parsedDueDate = result;
                }
                else
                {
                    return $"Invalid due date format: '{dueDate}'. Please use formats like 'tomorrow', 'next week', or 'YYYY-MM-DD'.";
                }
            }

            // Create the task
            var newTask = new TodoTask
            {
                Title = noteContent,
                Body = !string.IsNullOrWhiteSpace(details) ? new ItemBody
                {
                    Content = details,
                    ContentType = BodyType.Text
                } : null,
                DueDateTime = parsedDueDate.HasValue ? new DateTimeTimeZone
                {
                    DateTime = parsedDueDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                    TimeZone = TimeZoneInfo.Local.Id
                } : null,
                Importance = priority.ToLower() switch
                {
                    "high" => Microsoft.Graph.Models.Importance.High,
                    "low" => Microsoft.Graph.Models.Importance.Low,
                    _ => Microsoft.Graph.Models.Importance.Normal
                }
            };

            var createdTask = await graphClient.Me.Todo.Lists[targetList.Id].Tasks.PostAsync(newTask);

            return $"✅ Task created successfully for {userName}!\n" +
                   $"📝 Task: {createdTask?.Title}\n" +
                   $"📋 List: {targetList.DisplayName}\n" +
                   $"🔗 Priority: {priority}\n" +
                   (parsedDueDate.HasValue ? $"📅 Due: {parsedDueDate.Value:MMM dd, yyyy}\n" : "") +
                   $"🌐 View: https://to-do.office.com/tasks/{createdTask?.Id}/details";
        }
        catch (Exception ex)
        {
            return $"❌ Error creating task: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get recent tasks (To Do tasks). Use this when user asks for 'tasks', 'my tasks', 'recent tasks', 'show tasks', 'todos', etc.")]
    public async Task<string> GetRecentNotes(Kernel kernel,
        [Description("Number of recent tasks to retrieve (default 10)")] int count = 10,
        [Description("Include completed tasks (default false)")] bool includeCompleted = false,
        [Description("Task list name (optional, searches all lists if not specified)")] string listName = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";
            
            _logger.LogInformation("⏱️ GetRecentNotes started for user {UserName} with count={Count}, includeCompleted={IncludeCompleted}, listName={ListName}", 
                userName, count, includeCompleted, listName);

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to retrieve tasks - user authentication required.";
            }

            _logger.LogInformation("⏱️ GetRecentNotes: Creating Graph client for user {UserName} at {ElapsedMs}ms", userName, stopwatch.ElapsedMilliseconds);
            var graphClient = await CreateClientAsync(userAccessToken);
            _logger.LogInformation("⏱️ GetRecentNotes: Graph client created in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds);

            var allTasks = new List<(TodoTask Task, string ListName)>();

            if (!string.IsNullOrEmpty(listName))
            {
                // Search specific list
                try
                {
                    var taskList = await GetTaskList(graphClient, listName);
                    if (taskList?.Id != null)
                    {
                        var tasks = await GetTasksFromList(graphClient, taskList.Id, includeCompleted);
                        allTasks.AddRange(tasks.Select(t => (t, taskList.DisplayName ?? listName)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not find task list '{ListName}' for user {UserName}", listName, userName);
                    return $"Task list '{listName}' not found for {userName}.";
                }
            }
            else
            {
                // Get all lists
                var listFetchStartTime = stopwatch.ElapsedMilliseconds;
                _logger.LogInformation("⏱️ GetRecentNotes: Fetching all task lists for user {UserName} at {ElapsedMs}ms", userName, stopwatch.ElapsedMilliseconds);
                var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
                _logger.LogInformation("⏱️ GetRecentNotes: Task lists retrieved in {ElapsedMs}ms (total: {TotalMs}ms) - found {ListCount} lists", 
                    stopwatch.ElapsedMilliseconds - listFetchStartTime, stopwatch.ElapsedMilliseconds, taskLists?.Value?.Count ?? 0);
                
                if (taskLists?.Value?.Any() == true)
                {
                    foreach (var list in taskLists.Value)
                    {
                        if (list.Id != null)
                        {
                            var listProcessingStartTime = stopwatch.ElapsedMilliseconds;
                            _logger.LogInformation("⏱️ GetRecentNotes: Processing list '{ListName}' (ID: {ListId}) for user {UserName} at {ElapsedMs}ms", 
                                list.DisplayName, list.Id, userName, stopwatch.ElapsedMilliseconds);
                            
                            var tasks = await GetTasksFromList(graphClient, list.Id, includeCompleted);
                            allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? "Unknown")));
                            
                            _logger.LogInformation("⏱️ GetRecentNotes: List '{ListName}' tasks retrieved in {ElapsedMs}ms - found {TaskCount} tasks (total: {TotalMs}ms)", 
                                list.DisplayName, stopwatch.ElapsedMilliseconds - listProcessingStartTime, tasks.Count, stopwatch.ElapsedMilliseconds);
                        }
                    }
                }
            }

            _logger.LogInformation("⏱️ GetRecentNotes: Total tasks found across all lists: {TaskCount} for user {UserName} at {ElapsedMs}ms", 
                allTasks.Count, userName, stopwatch.ElapsedMilliseconds);

            if (!allTasks.Any())
            {
                return $"No {(includeCompleted ? "" : "incomplete ")}tasks found for {userName}.";
            }

            // Sort by creation time (most recent first) and take the requested count (max 10 for performance)
            var processingStartTime = stopwatch.ElapsedMilliseconds;
            var recentTasks = allTasks
                .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTimeOffset.MinValue)
                .Take(Math.Min(count, 10))
                .ToList();
                
            _logger.LogInformation("⏱️ GetRecentNotes: Task sorting and filtering completed in {ElapsedMs}ms - returning {ReturnCount} tasks (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - processingStartTime, recentTasks.Count, stopwatch.ElapsedMilliseconds);

            // Create task cards similar to calendar cards
            var cardCreationStartTime = stopwatch.ElapsedMilliseconds;
            var taskCards = recentTasks.Select(t => new
                {
                id = t.Task.Id,
                title = t.Task.Title ?? "Untitled Task",
                content = t.Task.Body?.Content?.Length > 150 ? 
                    t.Task.Body.Content.Substring(0, 150) + "..." : 
                    t.Task.Body?.Content ?? "",
                status = t.Task.Status?.ToString() ?? "NotStarted",
                priority = t.Task.Importance?.ToString() ?? "Normal",
                dueDate = t.Task.DueDateTime?.DateTime,
                dueDateFormatted = t.Task.DueDateTime?.DateTime != null ? 
                    DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
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

            var result = $"TASK_CARDS: {JsonSerializer.Serialize(taskCards, new JsonSerializerOptions { WriteIndented = false })}";
            _logger.LogInformation("⏱️ GetRecentNotes: Card creation completed in {ElapsedMs}ms (total: {TotalMs}ms)", 
                stopwatch.ElapsedMilliseconds - cardCreationStartTime, stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("⏱️ GetRecentNotes: TOTAL FUNCTION TIME: {TotalMs}ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("GetRecentNotes: Returning result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error getting recent tasks: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search tasks (To Do tasks) by content")]
    public async Task<string> SearchNotes(Kernel kernel,
        [Description("Search query to find in task titles and content")] string searchQuery,
        [Description("Maximum number of results (default 10)")] int maxResults = 10,
        [Description("Include completed tasks in search (default false)")] bool includeCompleted = false)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to search tasks - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return "Please provide a search query to find tasks.";
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
                return $"No tasks found matching '{searchQuery}' for {userName}.";
            }

            // Create task cards for search results
            var taskCards = matchingTasks.Select(t => new
            {
                id = t.Task.Id,
                title = t.Task.Title ?? "Untitled Task",
                content = t.Task.Body?.Content?.Length > 150 ? 
                    t.Task.Body.Content.Substring(0, 150) + "..." : 
                    t.Task.Body?.Content ?? "",
                status = t.Task.Status?.ToString() ?? "NotStarted",
                priority = t.Task.Importance?.ToString() ?? "Normal",
                dueDate = t.Task.DueDateTime?.DateTime,
                dueDateFormatted = t.Task.DueDateTime?.DateTime != null ? 
                    DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
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

            return $"TASK_CARDS: {JsonSerializer.Serialize(taskCards, new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Exception ex)
        {
            return $"❌ Error searching tasks: {ex.Message}";
        }
    }

    [KernelFunction, Description("Mark a task as complete or update its status")]
    public async Task<string> UpdateNoteStatus(Kernel kernel,
        [Description("Task title or part of title to find")] string noteTitle,
        [Description("New status: notstarted, inprogress, completed, waitingonothers, or deferred")] string status = "completed")
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to update task - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(noteTitle))
            {
                return "Please provide the task title to update.";
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

                return $"✅ Task updated successfully for {userName}!\n" +
                       $"📝 Task: {foundTask.Task.Title}\n" +
                       $"🔄 Status: {status}\n" +
                       $"📋 List: {foundTask.ListName}";
            }
            catch (InvalidOperationException)
            {
                return $"❌ Task with title containing '{noteTitle}' not found for {userName}.";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error updating task: {ex.Message}";
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

        // Try standard formats
        var formats = new[]
        {
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out result))
            {
                return true;
            }
        }

        return DateTime.TryParse(input, out result);
    }

    private async Task<GraphServiceClient> CreateClientAsync(string userAccessToken)
    {
        // Use the injected GraphService to create client with On-Behalf-Of flow
        return await _graphService.CreateClientAsync(userAccessToken);
    }

    private GraphServiceClient CreateGraphClient(string userAccessToken)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userAccessToken);
        
        return new GraphServiceClient(httpClient);
    }
} 