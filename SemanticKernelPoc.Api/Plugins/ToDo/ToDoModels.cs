namespace SemanticKernelPoc.Api.Plugins.ToDo;

public record TaskResponse(
    string Title,
    string Body,
    string Status,
    string DueDate,
    string Importance,
    string ListName,
    bool IsCompleted
);

public record TaskSearchResult(
    string Title,
    string Status,
    string Priority,
    string DueDate,
    string List,
    string Created,
    string Content,
    string MatchReason = null
);

public record TaskListInfo(
    string Name,
    bool IsOwner,
    bool IsShared,
    string WellknownListName
);