using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Helpers;

namespace SemanticKernelPoc.Api.Plugins.ToDo;

public class ToDoPlugin : BaseGraphPlugin
{
    public ToDoPlugin(IGraphService graphService, IGraphClientFactory graphClientFactory, ILogger<ToDoPlugin> logger) 
        : base(graphService, graphClientFactory, logger)
    {
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
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            var validationError = ValidateRequiredParameter(noteContent, "Task content");
            if (validationError != null)
            {
                return validationError;
            }

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

            return CreateSuccessResponse("Task created", userName,
                ("📝 Task", createdTask?.Title),
                ("📋 List", targetList.DisplayName),
                ("🔗 Priority", priority),
                ("📅 Due", parsedDueDate?.ToString("MMM dd, yyyy")),
                ("🌐 View", $"https://to-do.office.com/tasks/{createdTask?.Id}/details"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return $"❌ Error creating task: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get recent tasks and to-do items from Microsoft To Do. Use this for queries specifically about tasks, like 'my tasks', 'show my To Do list', 'recent to-dos', or 'what are my tasks for this week'. For display purposes, use analysisMode=false. For summary/analysis requests like 'summarize my tasks', 'what are my tasks about', 'task summary', use analysisMode=true. This function is ONLY for tasks and should NOT be used for SharePoint sites, OneDrive files, emails, or calendar events.")]
    public async Task<string> GetRecentNotes(Kernel kernel,
        [Description("Number of recent tasks to retrieve (default 5, max 10). Use this when user specifies 'last 3 tasks', 'get 5 todos', etc.")] int count = 5,
        [Description("Include completed tasks (default false). Set to true when user asks for 'all tasks' or 'completed tasks'")] bool includeCompleted = false,
        [Description("Task list name (optional, searches all lists if not specified). Use when user asks for tasks from specific list")] string listName = null,
        [Description("Time period filter: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days like '7' for last 7 days")] string timePeriod = null,
        [Description("Filter by completion status: 'completed', 'incomplete', or null for all tasks based on includeCompleted parameter")] string completionStatus = null,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what are my tasks about'. Set to false for listing/displaying tasks. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            _logger.LogInformation("🎯 GetRecentNotes started for user {UserName} with parameters: count={Count}, includeCompleted={IncludeCompleted}, timePeriod={TimePeriod}, analysisMode={AnalysisMode}",
                userName, count, includeCompleted, timePeriod, analysisMode);

            var allTasks = new List<(TodoTask Task, string ListName)>();

            try
            {
                _logger.LogInformation("🔍 Starting task list retrieval for user {UserName}", userName);

                var taskLists = await graphClient.Me.Todo.Lists.GetAsync();

                if (taskLists?.Value?.Any() == true)
                {
                    foreach (var list in taskLists.Value)
                    {
                        _logger.LogInformation("📋 Processing task list: '{ListName}' (ID: {ListId})", list.DisplayName, list.Id);

                        // If a specific list name is provided, only process that list
                        if (!string.IsNullOrWhiteSpace(listName) && 
                            !list.DisplayName?.Contains(listName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            _logger.LogInformation("⏭️ Skipping list '{ListName}' - doesn't match filter '{FilterName}'", list.DisplayName, listName);
                            continue;
                        }

                        try
                        {
                            var listProcessingStartTime = stopwatch.ElapsedMilliseconds;
                            
                            // Get tasks from this list with proper pagination to avoid performance issues
                            var tasks = await GetTasksFromList(graphClient, list.Id, includeCompleted);
                            allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? "Unknown")));

                            _logger.LogInformation("✅ List '{ListName}' processed in {ElapsedMs}ms - found {TaskCount} tasks (total: {TotalMs}ms)",
                                list.DisplayName, stopwatch.ElapsedMilliseconds - listProcessingStartTime, tasks.Count, stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ ERROR ACCESSING TASKS from list '{ListName}' (ID: {ListId})", list.DisplayName, list.Id);
                            
                            if (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized"))
                            {
                                _logger.LogError("   🚫 Access denied to list '{ListName}' - skipping", list.DisplayName);
                                continue; // Skip this list but continue with others
                            }
                            throw; // Re-throw for other errors
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ No task lists found for user {UserName}", userName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CRITICAL ERROR: Failed to retrieve task lists for user {UserName}", userName);
                
                if (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized") || 
                    ex.Message.Contains("insufficient privileges") || ex.Message.Contains("Access denied"))
                {
                    return $"❌ Authentication Error: Access denied to Microsoft To Do. Please ensure you have the necessary permissions and try signing in again.";
                }
                
                if (ex.Message.Contains("MsalUiRequiredException") || ex.Message.Contains("additional consent"))
                {
                    return $"❌ Authentication Error: Additional consent required for Microsoft To Do access. Please sign out and sign back in.";
                }
                
                return $"❌ Error: Failed to retrieve task lists for {userName}. {ex.Message}";
            }

            _logger.LogInformation("✅ GetRecentNotes: Total tasks found across all lists: {TaskCount} for user {UserName} at {ElapsedMs}ms",
                allTasks.Count, userName, stopwatch.ElapsedMilliseconds);

            if (!allTasks.Any())
            {
                _logger.LogInformation("⚠️ GetRecentNotes: No tasks found for user {UserName}", userName);
                return $"No tasks found for {userName}. Create some tasks in Microsoft To Do to get started!";
            }

            // Apply time period filter if specified
            var filteredTasks = allTasks.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue && endDate.HasValue)
                {
                    filteredTasks = allTasks.Where(t => 
                        t.Task.CreatedDateTime.HasValue &&
                        t.Task.CreatedDateTime.Value.Date >= startDate.Value.Date &&
                        t.Task.CreatedDateTime.Value.Date <= endDate.Value.Date);
                    
                    _logger.LogInformation("📅 Applied time filter '{TimePeriod}': {StartDate} to {EndDate}", 
                        timePeriod, startDate.Value.ToString("yyyy-MM-dd"), endDate.Value.ToString("yyyy-MM-dd"));
                }
            }

            // Apply completion status filter if specified
            if (!string.IsNullOrWhiteSpace(completionStatus))
            {
                filteredTasks = completionStatus.ToLower() switch
                {
                    "completed" => filteredTasks.Where(t => t.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed),
                    "incomplete" => filteredTasks.Where(t => t.Task.Status != Microsoft.Graph.Models.TaskStatus.Completed),
                    _ => filteredTasks
                };
                
                _logger.LogInformation("🎯 Applied completion status filter: {CompletionStatus}", completionStatus);
            }

            // Sort by creation date (most recent first) and take the requested count
            var recentTasks = filteredTasks
                .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTime.MinValue)
                .Take(Math.Min(count, 10)) // Limit to max 10 for performance
                .ToList();

            _logger.LogInformation("✅ GetRecentNotes: Task processing and filtering completed in {ElapsedMs}ms - {TaskCount} tasks after filtering (total: {TotalMs}ms)",
                stopwatch.ElapsedMilliseconds, recentTasks.Count, stopwatch.ElapsedMilliseconds);

            // Handle analysis vs card mode response
            if (analysisMode)
            {
                // For analysis mode, use AI to summarize actual task content
                var taskContents = recentTasks.Select(taskData => new
                {
                    Title = taskData.Task.Title ?? "Untitled Task",
                    Content = taskData.Task.Body?.Content ?? "",
                    Status = taskData.Task.Status?.ToString() ?? "NotStarted",
                    Priority = taskData.Task.Importance?.ToString() ?? "Normal",
                    DueDate = taskData.Task.DueDateTime?.DateTime != null ? 
                        DateTime.Parse(taskData.Task.DueDateTime.DateTime).ToString("yyyy-MM-dd") : "No due date",
                    ListName = taskData.ListName,
                    IsCompleted = taskData.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    Created = taskData.Task.CreatedDateTime?.ToString("yyyy-MM-dd") ?? "Unknown"
                }).ToList();

                // Create a prompt for AI to summarize the tasks
                var taskSummaryPrompt = $@"Please provide a concise summary of these {taskContents.Count} recent tasks for the user. Focus on the main topics, priorities, deadlines, and actionable items. Don't just list metadata - summarize what the tasks are actually about:

{string.Join("\n\n", taskContents.Select((task, i) => 
    $"Task {i + 1}:\n" +
    $"Title: {task.Title}\n" +
    $"Content: {task.Content}\n" +
    $"Status: {task.Status}\n" +
    $"Priority: {task.Priority}\n" +
    $"Due Date: {task.DueDate}\n" +
    $"List: {task.ListName}\n" +
    $"Created: {task.Created}"))}

Provide a helpful summary that tells the user what these tasks are about, their priorities, and any upcoming deadlines.";

                // Use the global kernel to get AI summary
                var globalKernel = kernel.Services.GetService<Kernel>();
                if (globalKernel != null)
                {
                    try
                    {
                        var summaryResult = await globalKernel.InvokePromptAsync(taskSummaryPrompt);
                        return summaryResult.ToString();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate AI summary for tasks");
                        // Fallback to simple summary
                        var completedCount = taskContents.Count(t => t.IsCompleted);
                        var highPriorityCount = taskContents.Count(t => t.Priority?.ToLower() == "high");
                        var dueSoonCount = taskContents.Count(t => !string.IsNullOrEmpty(t.DueDate) && t.DueDate != "No due date" && 
                            DateTime.TryParse(t.DueDate, out var due) && due <= DateTime.Now.AddDays(7));
                        
                        return $"Here's a summary of your {taskContents.Count} recent tasks:\n\n" +
                               $"• {completedCount} completed, {taskContents.Count - completedCount} pending\n" +
                               $"• {highPriorityCount} high priority tasks\n" +
                               $"• {dueSoonCount} due within the next week\n" +
                               $"• Recent tasks: {string.Join(", ", taskContents.Take(3).Select(t => t.Title))}\n\n" +
                               "Check your task list for full details.";
                    }
                }
                else
                {
                    // Fallback when no global kernel
                    var completedCount = taskContents.Count(t => t.IsCompleted);
                    var highPriorityCount = taskContents.Count(t => t.Priority?.ToLower() == "high");
                    return $"Found {taskContents.Count} tasks for {userName}. " +
                           $"{completedCount} completed, {highPriorityCount} high priority. " +
                           $"Most recent tasks: {string.Join(", ", taskContents.Take(3).Select(t => t.Title))}.";
                }
            }
            else
            {
                // Create task cards for card display mode
                var taskCards = recentTasks.Select((taskData, index) => new
                {
                    id = $"task_{index}_{taskData.Task.Id?.GetHashCode().ToString("X")}",
                    title = taskData.Task.Title?.Length > 80 ? taskData.Task.Title[..80] + "..." : taskData.Task.Title ?? "Untitled Task",
                    content = taskData.Task.Body?.Content?.Length > 120 ? taskData.Task.Body.Content[..120] + "..." : taskData.Task.Body?.Content ?? "",
                    dueDate = taskData.Task.DueDateTime?.DateTime != null ? 
                        DateTime.Parse(taskData.Task.DueDateTime.DateTime).ToString("yyyy-MM-dd") : null,
                    dueDateFormatted = taskData.Task.DueDateTime?.DateTime != null ?
                        DateTime.Parse(taskData.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                    isCompleted = taskData.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    priority = taskData.Task.Importance?.ToString() ?? "Normal",
                    listName = taskData.ListName,
                    status = taskData.Task.Status?.ToString() ?? "NotStarted",
                    created = taskData.Task.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                    createdDateTime = taskData.Task.CreatedDateTime,
                    webLink = $"https://to-do.office.com/tasks/{taskData.Task.Id}/details",
                    priorityColor = taskData.Task.Importance?.ToString()?.ToLower() switch
                    {
                        "high" => "#ef4444",
                        "low" => "#10b981", 
                        _ => "#6b7280"
                    },
                    statusColor = taskData.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed ? "#10b981" : "#f59e0b"
                }).ToList();

                var functionResponse = $"Found {taskCards.Count} recent tasks for {userName}.";

                // Use the direct kernel.Data approach like MailPlugin
                kernel.Data["TaskCards"] = taskCards;
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "tasks";
                kernel.Data["StructuredDataCount"] = taskCards.Count;
                kernel.Data["TaskFunctionResponse"] = functionResponse;

                return functionResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error in GetRecentNotes for user");
            return $"❌ Error retrieving tasks: {ex.Message}";
        }
        finally
        {
            _logger.LogInformation("⏱️ GetRecentNotes: TOTAL FUNCTION TIME: {TotalMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    [KernelFunction, Description("Search tasks and to-do items in Microsoft To Do by content, keywords, or title. Use this for queries like 'search my tasks for [keyword]', 'find To Do items about [topic]'. For display purposes, use analysisMode=false. For analysis of search results like 'summarize tasks about project', use analysisMode=true. This function is ONLY for tasks and should NOT be used for SharePoint sites, OneDrive files, emails, or calendar events.")]
    public async Task<string> SearchNotes(Kernel kernel,
        [Description("Search query to find in task titles and content. Can be keywords, phrases, or specific text")] string searchQuery,
        [Description("Maximum number of results (default 5, max 10)")] int maxResults = 5,
        [Description("Include completed tasks in search (default false). Set to true when user wants to search all tasks")] bool includeCompleted = false,
        [Description("Time period to search within: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days")] string timePeriod = null,
        [Description("Search in specific task list only (optional)")] string listName = null,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what are my tasks about'. Set to false for listing/displaying tasks. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
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

            var graphClient = await CreateClientAsync(userAccessToken);

            var allTasks = new List<(TodoTask Task, string ListName)>();

            // Get all lists or specific list
            var taskLists = await graphClient.Me.Todo.Lists.GetAsync();
            if (taskLists?.Value?.Any() == true)
            {
                var listsToSearch = taskLists.Value;
                
                // Filter to specific list if requested
                if (!string.IsNullOrWhiteSpace(listName))
                {
                    listsToSearch = taskLists.Value.Where(l => 
                        l.DisplayName?.Contains(listName, StringComparison.OrdinalIgnoreCase) == true).ToList();
                    
                    if (!listsToSearch.Any())
                    {
                        return $"Task list '{listName}' not found for {userName}.";
                    }
                }

                foreach (var list in listsToSearch)
                {
                    if (list.Id != null)
                    {
                        var tasks = await GetTasksFromList(graphClient, list.Id, includeCompleted);
                        allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? "Unknown")));
                    }
                }
            }

            if (!allTasks.Any())
            {
                return $"No tasks found for {userName}.";
            }

            // Filter by search query
            var matchingTasks = allTasks.Where(t =>
                (t.Task.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) ||
                (t.Task.Body?.Content?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

            // Apply time period filter if specified
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue || endDate.HasValue)
                {
                    matchingTasks = matchingTasks.Where(t =>
                    {
                        var createdDate = t.Task.CreatedDateTime?.DateTime;
                        if (!createdDate.HasValue) return false;
                        
                        if (startDate.HasValue && createdDate < startDate.Value) return false;
                        if (endDate.HasValue && createdDate > endDate.Value) return false;
                        
                        return true;
                    }).ToList();
                }
            }

            // Sort by creation time and limit results
            var searchResults = matchingTasks
                .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTimeOffset.MinValue)
                .Take(Math.Min(maxResults, 10))
                .ToList();

            if (!searchResults.Any())
            {
                return $"No tasks were found matching your search query: '{searchQuery}'.";
            }

            if (analysisMode)
            {
                // For analysis mode, return full content without truncation and no technical IDs
                var analysisData = searchResults.Select(t => new
                {
                    title = t.Task.Title ?? "Untitled Task",
                    content = t.Task.Body?.Content ?? "",
                    status = t.Task.Status?.ToString() ?? "NotStarted",
                    priority = t.Task.Importance?.ToString() ?? "Normal",
                    dueDate = t.Task.DueDateTime?.DateTime,
                    dueDateFormatted = t.Task.DueDateTime?.DateTime != null ?
                        DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                    created = t.Task.CreatedDateTime?.ToString("MMM dd, yyyy") ?? "Unknown",
                    isCompleted = t.Task.Status?.ToString()?.ToLower() == "completed",
                    matchReason = (t.Task.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) ? "Title" : "Content",
                    listName = t.ListName
                }).ToList();

                // For analysis mode, don't store structured data - return text only
                var completedCount = analysisData.Count(t => t.isCompleted);
                var highPriorityCount = analysisData.Count(t => t.priority?.ToLower() == "high");
                
                return $"Found {analysisData.Count} tasks matching '{searchQuery}' for {userName}. " +
                       $"{completedCount} completed, {highPriorityCount} high priority. " +
                       $"Match types: {string.Join(", ", analysisData.GroupBy(t => t.matchReason).Select(g => $"{g.Count()} by {g.Key}"))}";
            }
            else
            {
                // Create task cards for search results
                var taskCards = searchResults.Select((t, index) => new
                {
                    id = $"search_{index}_{t.Task.Id?.GetHashCode().ToString("X")}",
                    title = t.Task.Title ?? "Untitled Task",
                    content = t.Task.Body?.Content?.Length > 100 ?
                        t.Task.Body.Content[..100] + "..." :
                        t.Task.Body?.Content ?? "",
                    status = t.Task.Status?.ToString() ?? "NotStarted",
                    priority = t.Task.Importance?.ToString() ?? "Normal",
                    dueDate = t.Task.DueDateTime?.DateTime,
                    dueDateFormatted = t.Task.DueDateTime?.DateTime != null ?
                        DateTime.Parse(t.Task.DueDateTime.DateTime).ToString("MMM dd, yyyy") : null,
                    created = t.Task.CreatedDateTime?.ToString("MMM dd, yyyy") ?? "Unknown",
                    isCompleted = t.Task.Status?.ToString()?.ToLower() == "completed",
                    matchReason = (t.Task.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) ? "Title" : "Content",
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

                // Store structured data in kernel data for the system to process
                kernel.Data["TaskCards"] = taskCards;
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "tasks";
                kernel.Data["StructuredDataCount"] = taskCards.Count;
                kernel.Data["TaskFunctionResponse"] = $"Found {taskCards.Count} tasks matching '{searchQuery}' for {userName}.";

                return $"Found {taskCards.Count} tasks matching '{searchQuery}' for {userName}.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchNotes for user {UserName} with query '{SearchQuery}'", kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "Unknown", searchQuery);
            return $"Error searching tasks: {ex.Message}";
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

            var graphClient = await CreateClientAsync(userAccessToken);

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

            var graphClient = await CreateClientAsync(userAccessToken);

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

    private static async Task<TodoTaskList> GetTaskList(GraphServiceClient graphClient, string listName)
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
            l.DisplayName?.Equals(listName, StringComparison.OrdinalIgnoreCase) == true) ?? throw new InvalidOperationException($"Task list '{listName}' not found");
        return foundList;
    }

    private static async Task<List<TodoTask>> GetTasksFromList(GraphServiceClient graphClient, string listId, bool includeCompleted)
    {
        var tasks = await graphClient.Me.Todo.Lists[listId].Tasks.GetAsync(requestConfig =>
        {
            if (!includeCompleted)
            {
                requestConfig.QueryParameters.Filter = "status ne 'completed'";
            }
            requestConfig.QueryParameters.Top = 50;
            requestConfig.QueryParameters.Orderby = ["createdDateTime desc"];
        });

        return tasks?.Value?.ToList() ?? [];
    }

    private static async Task<(TodoTask Task, string ListId, string ListName)> FindTaskByTitle(GraphServiceClient graphClient, string titleSearch)
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
                t.Title?.ToLower().Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) == true);

            if (matchingTask != null)
            {
                return (matchingTask, list.Id, list.DisplayName ?? "Unknown");
            }
        }

        throw new InvalidOperationException($"No task found with title containing '{titleSearch}'");
    }

    private static bool TryParseDate(string input, out DateTime result)
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
        return await _graphService.CreateClientAsync(userAccessToken);
    }

    private static (DateTime? startDate, DateTime? endDate) ParseTimePeriod(string timePeriod)
    {
        if (string.IsNullOrWhiteSpace(timePeriod))
            return (null, null);

        var now = DateTime.UtcNow;
        var today = now.Date;

        return timePeriod.ToLower().Trim() switch
        {
            "today" => (today, today.AddDays(1)),
            "yesterday" => (today.AddDays(-1), today),
            "this_week" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(7 - (int)today.DayOfWeek)),
            "last_week" => (today.AddDays(-7 - (int)today.DayOfWeek), today.AddDays(-(int)today.DayOfWeek)),
            "this_month" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            "last_month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1)),
            _ when int.TryParse(timePeriod, out var days) && days > 0 => (today.AddDays(-days), null),
            _ => (null, null)
        };
    }
}