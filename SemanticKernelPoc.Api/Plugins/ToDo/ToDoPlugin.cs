using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Shared;
using SharedConstants = SemanticKernelPoc.Api.Services.Shared.Constants;

namespace SemanticKernelPoc.Api.Plugins.ToDo;

public class ToDoPlugin : BaseGraphPlugin
{
    private readonly ICardBuilderService _cardBuilder;
    private readonly IAnalysisModeService _analysisMode;
    private readonly ITextProcessingService _textProcessor;

    public ToDoPlugin(
        IGraphService graphService, 
        IGraphClientFactory graphClientFactory, 
        ILogger<ToDoPlugin> logger,
        ICardBuilderService cardBuilder,
        IAnalysisModeService analysisMode,
        ITextProcessingService textProcessor) 
        : base(graphService, graphClientFactory, logger)
    {
        _cardBuilder = cardBuilder;
        _analysisMode = analysisMode;
        _textProcessor = textProcessor;
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
            var targetList = await GetOrCreateTargetList(graphClient, listName, userName);
            if (targetList == null)
            {
                return $"No task lists found for {userName}. Please create a task list first.";
            }

            // Parse due date if provided
            var parsedDueDate = _textProcessor.ParseDueDate(dueDate);
            if (parsedDueDate.HasError)
            {
                return parsedDueDate.ErrorMessage;
            }

            // Create the task
            var newTask = CreateNewTask(noteContent, details, parsedDueDate.Date, priority);
            var createdTask = await graphClient.Me.Todo.Lists[targetList.Id].Tasks.PostAsync(newTask);

            return CreateSuccessResponse("Task created", userName,
                ("📝 Task", createdTask?.Title),
                ("📋 List", targetList.DisplayName),
                ("🔗 Priority", priority),
                ("📅 Due", parsedDueDate.Date?.ToString(SharedConstants.DateFormats.FriendlyDate)),
                ("🌐 View", SharedConstants.ServiceUrls.GetToDoTaskUrl(createdTask?.Id ?? "")));
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
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, advice, recommendations, priorities, or decision help. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview, which task, what should I, recommend, priority, prioritize, tackle first, focus on, start with, most important, urgent, advice, suggest, help me decide, what to do. Set to false ONLY for simple listing/displaying tasks.")] bool analysisMode = false)
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

            // Get all tasks across lists
            var allTasks = await GetTasksFromAllLists(graphClient, listName, includeCompleted, userName);
            
            if (!allTasks.Any())
            {
                _logger.LogInformation("⚠️ GetRecentNotes: No tasks found for user {UserName}", userName);
                return $"No tasks found for {userName}. Create some tasks in Microsoft To Do to get started!";
            }

            // Apply filters and get recent tasks
            var recentTasks = FilterAndSortTasks(allTasks, timePeriod, completionStatus, count);

            _logger.LogInformation("✅ GetRecentNotes: Task processing and filtering completed in {ElapsedMs}ms - {TaskCount} tasks after filtering",
                stopwatch.ElapsedMilliseconds, recentTasks.Count);

            // DEBUG: Log details about the filtered tasks
            _logger.LogInformation("🔍 GetRecentNotes DEBUG: Recent tasks details:");
            for (int i = 0; i < Math.Min(3, recentTasks.Count); i++)
            {
                var task = recentTasks[i];
                _logger.LogInformation("  Task {Index}: ID={TaskId}, Title={Title}, Status={Status}", 
                    i, task.Task.Id, task.Task.Title, task.Task.Status);
            }

            // Handle analysis vs card mode response
            if (analysisMode)
            {
                // Clear any existing card data when doing analysis to ensure analysis takes precedence
                kernel.Data.Remove("TasksCards");
                kernel.Data.Remove("HasStructuredData");
                kernel.Data.Remove("StructuredDataType");
                kernel.Data.Remove("StructuredDataCount");
                kernel.Data.Remove("TasksFunctionResponse");
                
                _logger.LogInformation("🔍 GetRecentNotes: Starting analysis mode - cleared any existing card data");
                
                return await _analysisMode.GenerateAISummaryAsync(
                    kernel,
                    recentTasks,
                    "tasks",
                    userName,
                    taskData => new
                    {
                        Title = taskData.Task.Title ?? SharedConstants.DefaultText.UntitledTask,
                        Content = taskData.Task.Body?.Content ?? "",
                        Status = taskData.Task.Status?.ToString() ?? SharedConstants.DefaultText.NotStarted,
                        Priority = taskData.Task.Importance?.ToString() ?? SharedConstants.DefaultText.Normal,
                        DueDate = _textProcessor.FormatTaskDueDate(taskData.Task.DueDateTime),
                        ListName = taskData.ListName,
                        IsCompleted = taskData.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                        Created = taskData.Task.CreatedDateTime?.ToString(SharedConstants.DateFormats.StandardDate) ?? SharedConstants.DefaultText.Unknown
                    });
            }
            else
            {
                _logger.LogInformation("🔍 GetRecentNotes: Building task cards for {TaskCount} tasks", recentTasks.Count);
                var taskCards = _cardBuilder.BuildTaskCards(recentTasks, CreateTaskCard);
                _logger.LogInformation("🔍 GetRecentNotes: Built {CardCount} task cards", taskCards.Count);
                
                var functionResponse = $"Found {taskCards.Count} recent tasks for {userName}.";

                _cardBuilder.SetCardData(kernel, "tasks", taskCards, taskCards.Count, functionResponse);
                _logger.LogInformation("🔍 GetRecentNotes: Set card data in kernel with {CardCount} cards", taskCards.Count);
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
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return "Please provide a search query to find tasks.";
            }

            // Get all tasks and filter by search
            var allTasks = await GetTasksFromAllLists(graphClient, listName, includeCompleted, userName);
            var matchingTasks = FilterTasksBySearch(allTasks, searchQuery, timePeriod, maxResults);

            if (!matchingTasks.Any())
            {
                return $"No tasks were found matching your search query: '{searchQuery}'.";
            }

            if (analysisMode)
            {
                return await _analysisMode.GenerateAISummaryAsync(
                    kernel,
                    matchingTasks,
                    $"search results for '{searchQuery}'",
                    userName,
                    t => new
                    {
                        title = t.Task.Title ?? SharedConstants.DefaultText.UntitledTask,
                        content = t.Task.Body?.Content ?? "",
                        status = t.Task.Status?.ToString() ?? SharedConstants.DefaultText.NotStarted,
                        priority = t.Task.Importance?.ToString() ?? SharedConstants.DefaultText.Normal,
                        dueDate = _textProcessor.FormatTaskDueDate(t.Task.DueDateTime),
                        created = t.Task.CreatedDateTime?.ToString(SharedConstants.DateFormats.FriendlyDate) ?? SharedConstants.DefaultText.Unknown,
                        isCompleted = t.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                        matchReason = DetermineTaskMatchReason(t.Task, searchQuery),
                        listName = t.ListName
                    });
            }
            else
            {
                var taskCards = _cardBuilder.BuildTaskCards(matchingTasks, (task, index) => CreateSearchTaskCard(task, index, searchQuery));
                var functionResponse = $"Found {taskCards.Count} tasks matching '{searchQuery}' for {userName}.";

                _cardBuilder.SetCardData(kernel, "tasks", taskCards, taskCards.Count, functionResponse);
                return functionResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchNotes for user {UserName} with query '{SearchQuery}'", 
                kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "Unknown", searchQuery);
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
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            if (string.IsNullOrWhiteSpace(noteTitle))
            {
                return "Please provide the task title to update.";
            }

            try
            {
                var foundTask = await FindTaskByTitle(graphClient, noteTitle);
                var updateTask = new TodoTask
                {
                    Status = ParseTaskStatus(status)
                };

                await graphClient.Me.Todo.Lists[foundTask.ListId].Tasks[foundTask.Task.Id].PatchAsync(updateTask);

                return CreateSuccessResponse("Task updated", userName,
                    ("📝 Task", foundTask.Task.Title),
                    ("🔄 Status", status),
                    ("📋 List", foundTask.ListName));
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
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

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

    #region Private Helper Methods

    private async Task<TodoTaskList?> GetOrCreateTargetList(GraphServiceClient graphClient, string? listName, string userName)
    {
        var taskLists = await graphClient.Me.Todo.Lists.GetAsync();

        if (taskLists?.Value?.Any() != true)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(listName))
        {
            var foundList = taskLists.Value.FirstOrDefault(l => 
                l.DisplayName?.Contains(listName, StringComparison.OrdinalIgnoreCase) == true);
            
            if (foundList == null)
            {
                throw new InvalidOperationException($"Task list '{listName}' not found for {userName}. Please check the list name or create the list first.");
            }
            
            return foundList;
        }

        // Return default list
        return taskLists.Value.FirstOrDefault(l => l.WellknownListName == WellknownListName.DefaultList) ??
               taskLists.Value.FirstOrDefault();
    }

    private TodoTask CreateNewTask(string noteContent, string? details, DateTime? dueDate, string priority)
    {
        return new TodoTask
        {
            Title = noteContent,
            Body = !string.IsNullOrWhiteSpace(details) ? new ItemBody
            {
                Content = details,
                ContentType = BodyType.Text
            } : null,
            DueDateTime = dueDate.HasValue ? new DateTimeTimeZone
            {
                DateTime = dueDate.Value.ToString(SharedConstants.DateFormats.GraphApiDateTime),
                TimeZone = TimeZoneInfo.Local.Id
            } : null,
            Importance = ParseTaskImportance(priority)
        };
    }

    private async Task<List<(TodoTask Task, string ListName)>> GetTasksFromAllLists(
        GraphServiceClient graphClient, 
        string? listName, 
        bool includeCompleted, 
        string userName)
    {
        var allTasks = new List<(TodoTask Task, string ListName)>();

        try
        {
            var taskLists = await graphClient.Me.Todo.Lists.GetAsync();

            if (taskLists?.Value?.Any() == true)
            {
                foreach (var list in taskLists.Value)
                {
                    // Filter to specific list if requested
                    if (!string.IsNullOrWhiteSpace(listName) && 
                        !list.DisplayName?.Contains(listName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        continue;
                    }

                    try
                    {
                        var tasks = await GetTasksFromList(graphClient, list.Id!, includeCompleted);
                        allTasks.AddRange(tasks.Select(t => (t, list.DisplayName ?? SharedConstants.DefaultText.Unknown)));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR ACCESSING TASKS from list '{ListName}' (ID: {ListId})", list.DisplayName, list.Id);
                        
                        if (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized"))
                        {
                            continue; // Skip this list but continue with others
                        }
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CRITICAL ERROR: Failed to retrieve task lists for user {UserName}", userName);
            
            if (ex.Message.Contains("Forbidden") || ex.Message.Contains("Unauthorized"))
            {
                throw new UnauthorizedAccessException("Access denied to Microsoft To Do. Please ensure you have the necessary permissions and try signing in again.");
            }
            
            throw;
        }

        return allTasks;
    }

    private List<(TodoTask Task, string ListName)> FilterAndSortTasks(
        List<(TodoTask Task, string ListName)> allTasks,
        string? timePeriod,
        string? completionStatus,
        int count)
    {
        var filteredTasks = allTasks.AsEnumerable();

        // Apply time period filter
        if (!string.IsNullOrWhiteSpace(timePeriod))
        {
            var (startDate, endDate) = _textProcessor.ParseTimePeriod(timePeriod);
            if (startDate.HasValue && endDate.HasValue)
            {
                filteredTasks = filteredTasks.Where(t => 
                    t.Task.CreatedDateTime.HasValue &&
                    t.Task.CreatedDateTime.Value.Date >= startDate.Value.Date &&
                    t.Task.CreatedDateTime.Value.Date <= endDate.Value.Date);
            }
        }

        // Apply completion status filter
        if (!string.IsNullOrWhiteSpace(completionStatus))
        {
            filteredTasks = completionStatus.ToLower() switch
            {
                "completed" => filteredTasks.Where(t => t.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed),
                "incomplete" => filteredTasks.Where(t => t.Task.Status != Microsoft.Graph.Models.TaskStatus.Completed),
                _ => filteredTasks
            };
        }

        return filteredTasks
            .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTime.MinValue)
            .Take(Math.Min(count, SharedConstants.QueryLimits.MaxTaskCount))
            .ToList();
    }

    private List<(TodoTask Task, string ListName)> FilterTasksBySearch(
        List<(TodoTask Task, string ListName)> allTasks,
        string searchQuery,
        string? timePeriod,
        int maxResults)
    {
        var matchingTasks = allTasks.Where(t =>
            (t.Task.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) ||
            (t.Task.Body?.Content?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true)
        );

        // Apply time period filter if specified
        if (!string.IsNullOrWhiteSpace(timePeriod))
        {
            var (startDate, endDate) = _textProcessor.ParseTimePeriod(timePeriod);
            if (startDate.HasValue || endDate.HasValue)
            {
                matchingTasks = matchingTasks.Where(t =>
                {
                    var createdDate = t.Task.CreatedDateTime?.DateTime;
                    if (!createdDate.HasValue) return false;
                    
                    if (startDate.HasValue && createdDate < startDate.Value) return false;
                    if (endDate.HasValue && createdDate > endDate.Value) return false;
                    
                    return true;
                });
            }
        }

        return matchingTasks
            .OrderByDescending(t => t.Task.CreatedDateTime ?? DateTimeOffset.MinValue)
            .Take(Math.Min(maxResults, SharedConstants.QueryLimits.MaxTaskCount))
            .ToList();
    }

    private object CreateTaskCard((TodoTask Task, string ListName) taskData, int index)
    {
        return new
        {
            id = $"task_{index}_{taskData.Task.Id?.GetHashCode():X}",
            title = _textProcessor.TruncateText(taskData.Task.Title, SharedConstants.TextLimits.TaskTitleMaxLength, SharedConstants.DefaultText.UntitledTask),
            content = _textProcessor.TruncateText(taskData.Task.Body?.Content, SharedConstants.TextLimits.TaskContentMaxLength),
            dueDate = _textProcessor.FormatTaskDueDate(taskData.Task.DueDateTime, SharedConstants.DateFormats.StandardDate),
            dueDateFormatted = _textProcessor.FormatTaskDueDate(taskData.Task.DueDateTime, SharedConstants.DateFormats.FriendlyDate),
            isCompleted = taskData.Task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
            priority = taskData.Task.Importance?.ToString() ?? SharedConstants.DefaultText.Normal,
            listName = taskData.ListName,
            status = taskData.Task.Status?.ToString() ?? SharedConstants.DefaultText.NotStarted,
            created = taskData.Task.CreatedDateTime?.ToString(SharedConstants.DateFormats.StandardDateTime) ?? SharedConstants.DefaultText.Unknown,
            createdDateTime = taskData.Task.CreatedDateTime,
            webLink = SharedConstants.ServiceUrls.GetToDoTaskUrl(taskData.Task.Id ?? ""),
            priorityColor = _textProcessor.GetPriorityColor(taskData.Task.Importance?.ToString()),
            statusColor = _textProcessor.GetTaskStatusColor(taskData.Task.Status?.ToString())
        };
    }

    private object CreateSearchTaskCard((TodoTask Task, string ListName) taskData, int index, string searchQuery)
    {
        var baseCard = CreateTaskCard(taskData, index);
        var cardDict = new Dictionary<string, object>();
        
        // Copy all properties from base card
        foreach (var property in baseCard.GetType().GetProperties())
        {
            cardDict[property.Name] = property.GetValue(baseCard)!;
        }
        
        // Update specific properties for search results
        cardDict["id"] = $"search_{index}_{taskData.Task.Id?.GetHashCode():X}";
        cardDict["matchReason"] = DetermineTaskMatchReason(taskData.Task, searchQuery);
        
        return cardDict;
    }

    private static string DetermineTaskMatchReason(TodoTask task, string searchQuery)
    {
        if (task.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) return "Title";
        if (task.Body?.Content?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) == true) return "Content";
        return "Search";
    }

    private static async Task<List<TodoTask>> GetTasksFromList(GraphServiceClient graphClient, string listId, bool includeCompleted)
    {
        var tasks = await graphClient.Me.Todo.Lists[listId].Tasks.GetAsync(requestConfig =>
        {
            if (!includeCompleted)
            {
                requestConfig.QueryParameters.Filter = "status ne 'completed'";
            }
            requestConfig.QueryParameters.Top = SharedConstants.QueryLimits.MaxTasksPerList;
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
                t.Title?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true);

            if (matchingTask != null)
            {
                return (matchingTask, list.Id, list.DisplayName ?? SharedConstants.DefaultText.Unknown);
            }
        }

        throw new InvalidOperationException($"No task found with title containing '{titleSearch}'");
    }

    private static Microsoft.Graph.Models.Importance ParseTaskImportance(string priority)
    {
        return priority.ToLower() switch
        {
            "high" => Microsoft.Graph.Models.Importance.High,
            "low" => Microsoft.Graph.Models.Importance.Low,
            _ => Microsoft.Graph.Models.Importance.Normal
        };
    }

    private static Microsoft.Graph.Models.TaskStatus ParseTaskStatus(string status)
    {
        return status.ToLower() switch
        {
            "notstarted" => Microsoft.Graph.Models.TaskStatus.NotStarted,
            "inprogress" => Microsoft.Graph.Models.TaskStatus.InProgress,
            "completed" => Microsoft.Graph.Models.TaskStatus.Completed,
            "waitingonothers" => Microsoft.Graph.Models.TaskStatus.WaitingOnOthers,
            "deferred" => Microsoft.Graph.Models.TaskStatus.Deferred,
            _ => Microsoft.Graph.Models.TaskStatus.Completed
        };
    }

    #endregion
}