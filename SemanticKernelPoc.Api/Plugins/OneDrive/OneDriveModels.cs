namespace SemanticKernelPoc.Api.Plugins.OneDrive;

public record OneDriveInfo(
    string Name,
    string DriveType,
    string TotalSpace,
    string UsedSpace,
    string WebUrl
);

public record OneDriveOperationResult(
    string Operation,
    string Status,
    string Note = null,
    string FolderName = null
);