using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.ToDo;

public class ToDoPlugin(IGraphService graphService, ILogger<ToDoPlugin> logger) : BaseGraphPlugin(graphService, logger)
{
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

            var graphClient = await CreateClientAsync(userAccessToken);

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

    [KernelFunction, Description("Get recent tasks and to-do items from Microsoft To Do. Use this for queries specifically about tasks, like 'my tasks', 'show my To Do list', 'recent to-dos', or 'what are my tasks for this week'. This function is ONLY for tasks and should NOT be used for SharePoint sites, OneDrive files, emails, or calendar events. Supports filtering by count, time period, completion status, and task list.")]
    public async Task<string> GetRecentNotes(Kernel kernel,
        [Description("Number of recent tasks to retrieve (default 5, max 10). Use this when user specifies 'last 3 tasks', 'get 5 todos', etc.")] int count = 5,
        [Description("Include completed tasks (default false). Set to true when user asks for 'all tasks' or 'completed tasks'")] bool includeCompleted = false,
        [Description("Task list name (optional, searches all lists if not specified). Use when user asks for tasks from specific list")] string listName = null,
        [Description("Time period filter: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days like '7' for last 7 days")] string timePeriod = null,
        [Description("Filter by completion status: 'completed', 'incomplete', or null for all tasks based on includeCompleted parameter")] string completionStatus = null,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            _logger.LogInformation("🎯 GetRecentNotes started for user {UserName} with parameters: count={Count}, includeCompleted={IncludeCompleted}, timePeriod={TimePeriod}, analysisMode={AnalysisMode}",
                userName, count, includeCompleted, timePeriod, analysisMode);

            if (string.IsNullOrEmpty(userAccessToken))
            {
                _logger.LogError("❌ No access token available for user {UserName}", userName);
                return $"Unable to retrieve tasks for {userName} - authentication required.";
            }

            var allTasks = new List<(TodoTask Task, string ListName)>();

            using (var graphClient = await CreateClientAsync(userAccessToken))
            {
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
                                _logger.LogError("   🔍 Exception Type: {ExceptionType}", ex.GetType().Name);
                                _logger.LogError("   📝 Exception Message: {Message}", ex.Message);
                                
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
                    _logger.LogError("   🔍 Exception Type: {ExceptionType}", ex.GetType().Name);
                    _logger.LogError("   📝 Exception Message: {Message}", ex.Message);
                    
                    if (ex.InnerException != null)
                    {
                        _logger.LogError("   🔗 Inner Exception: {InnerType} - {InnerMessage}", 
                            ex.InnerException.GetType().Name, ex.InnerException.Message);
                    }
                    
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
            }

            _logger.LogInformation("✅ GetRecentNotes: Total tasks found across all lists: {TaskCount} for user {UserName} at {ElapsedMs}ms",
                allTasks.Count, userName, stopwatch.ElapsedMilliseconds);

            if (!allTasks.Any())
            {
                _logger.LogInformation("ℹ️ No tasks found for user {UserName} with current filters", userName);
                return $"📝 No {(includeCompleted ? "" : "incomplete ")}tasks found for {userName}.";
            }

            // Sort by creation time (most recent first) and take the requested count (max 10 for performance)
            var processingStartTime = stopwatch.ElapsedMilliseconds;
            var recentTasks = allTasks
                .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTimeOffset.MinValue)
                .Take(Math.Min(count, 10))
                .ToList();

            // Apply additional filters if specified
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue || endDate.HasValue)
                {
                    recentTasks = recentTasks.Where(t =>
                    {
                        var createdDate = t.Task.CreatedDateTime?.DateTime;
                        if (!createdDate.HasValue) return false;
                        
                        if (startDate.HasValue && createdDate < startDate.Value) return false;
                        if (endDate.HasValue && createdDate > endDate.Value) return false;
                        
                        return true;
                    }).ToList();
                }
            }

            // Apply completion status filter if specified
            if (!string.IsNullOrWhiteSpace(completionStatus))
            {
                var isCompletedFilter = completionStatus.ToLower() == "completed";
                recentTasks = recentTasks.Where(t =>
                {
                    var isCompleted = t.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed;
                    return isCompletedFilter ? isCompleted : !isCompleted;
                }).ToList();
            }

            _logger.LogInformation("✅ GetRecentNotes: Task processing and filtering completed in {ElapsedMs}ms - {TaskCount} tasks after filtering (total: {TotalMs}ms)",
                stopwatch.ElapsedMilliseconds - processingStartTime, recentTasks.Count, stopwatch.ElapsedMilliseconds);

            // Return clean, natural language response without prefixes
            if (analysisMode)
            {
                // For analysis mode, don't store structured data - return text only
                // Create minimal task data just for analysis
                var taskSummary = recentTasks.Select(t => new
                {
                    title = t.Task.Title ?? "Untitled Task",
                    status = t.Task.Status?.ToString() ?? "NotStarted",
                    priority = t.Task.Importance?.ToString() ?? "Normal",
                    isCompleted = t.Task.Status?.ToString()?.ToLower() == "completed"
                }).ToList();
                
                var completedCount = taskSummary.Count(t => t.isCompleted);
                var highPriorityCount = taskSummary.Count(t => t.priority?.ToLower() == "high");
                
                var responseMessage = $"Found {taskSummary.Count} tasks for {userName}. " +
                       $"{completedCount} completed, {highPriorityCount} high priority. " +
                       $"Most recent tasks cover: {string.Join(", ", taskSummary.Take(3).Select(t => t.title))}.";
                
                _logger.LogInformation("⏱️ GetRecentNotes: Analysis mode - TOTAL FUNCTION TIME: {TotalMs}ms", stopwatch.ElapsedMilliseconds);
                return responseMessage;
            }
            else
            {
                // Create structured task data and store it in kernel data for processing
                var cardCreationStartTime = stopwatch.ElapsedMilliseconds;
                
                var taskCards = recentTasks.Select((t, index) => new
                {
                    id = $"task_{index}_{t.Task.Id?.GetHashCode().ToString("X")}",
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
                
                // Store the function response for the controller to use
                var responseMessage = $"Found {taskCards.Count} recent tasks for {userName}.";
                kernel.Data["TaskFunctionResponse"] = responseMessage;
                
                _logger.LogInformation("⏱️ GetRecentNotes: Card creation completed in {ElapsedMs}ms (total: {TotalMs}ms)",
                    stopwatch.ElapsedMilliseconds - cardCreationStartTime, stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("⏱️ GetRecentNotes: TOTAL FUNCTION TIME: {TotalMs}ms", stopwatch.ElapsedMilliseconds);
                
                return responseMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRecentNotes for user {UserName}", kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "Unknown");
            return $"Error retrieving tasks: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search tasks and to-do items in Microsoft To Do by content, keywords, or title. Use this for queries like 'search my tasks for [keyword]', 'find To Do items about [topic]'. This function is ONLY for tasks and should NOT be used for SharePoint sites, OneDrive files, emails, or calendar events. Supports filtering by max results, completion status, time period, and task list.")]
    public async Task<string> SearchNotes(Kernel kernel,
        [Description("Search query to find in task titles and content. Can be keywords, phrases, or specific text")] string searchQuery,
        [Description("Maximum number of results (default 5, max 10)")] int maxResults = 5,
        [Description("Include completed tasks in search (default false). Set to true when user wants to search all tasks")] bool includeCompleted = false,
        [Description("Time period to search within: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days")] string timePeriod = null,
        [Description("Search in specific task list only (optional)")] string listName = null,
        [Description("Analysis mode: set to true for summarization/analysis requests to get full content, false for card display (default false)")] bool analysisMode = false)
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