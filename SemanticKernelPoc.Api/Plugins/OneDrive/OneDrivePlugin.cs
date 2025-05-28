using Microsoft.SemanticKernel;
using System.ComponentModel;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.OneDrive;

public class OneDrivePlugin(IGraphService graphService, ILogger<OneDrivePlugin> logger) : BaseGraphPlugin(graphService, logger)
{
    [KernelFunction, Description("Get basic OneDrive information")]
    public async Task<string> GetOneDriveInfo(Kernel kernel)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var drive = await graphClient.Me.Drive.GetAsync();

                if (drive != null)
                {
                    var driveInfo = new
                    {
                        Name = drive.Name ?? "OneDrive",
                        DriveType = drive.DriveType?.ToString() ?? "Personal",
                        TotalSpace = drive.Quota?.Total.HasValue == true ? $"{drive.Quota.Total.Value / (1024 * 1024 * 1024)} GB" : "Unknown",
                        UsedSpace = drive.Quota?.Used.HasValue == true ? $"{drive.Quota.Used.Value / (1024 * 1024 * 1024)} GB" : "Unknown",
                        drive.WebUrl
                    };

                    return FormatJsonResponse(new[] { driveInfo }, userName, "OneDrive information");
                }

                return $"Could not retrieve OneDrive information for {userName}.";
            },
            "GetOneDriveInfo"
        );
    }

    [KernelFunction, Description("Get files from OneDrive (simplified)")]
    public async Task<string> GetOneDriveFiles(Kernel kernel)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var drive = await graphClient.Me.Drive.GetAsync();
                if (drive?.Id == null)
                {
                    return $"Could not access OneDrive for {userName}.";
                }

                return CreateSuccessResponse(
                    "OneDrive access verification",
                    userName,
                    ("💾 Drive", drive.Name ?? "OneDrive"),
                    ("📊 Total Space", drive.Quota?.Total.HasValue == true ? $"{drive.Quota.Total.Value / (1024 * 1024 * 1024)} GB" : "Unknown"),
                    ("📈 Used Space", drive.Quota?.Used.HasValue == true ? $"{drive.Quota.Used.Value / (1024 * 1024 * 1024)} GB" : "Unknown"),
                    ("📝 Note", "Advanced file browsing requires additional Graph API configuration")
                );
            },
            "GetOneDriveFiles"
        );
    }

    [KernelFunction, Description("OneDrive folder operations")]
    public async Task<string> OneDriveFolderOperations(Kernel kernel,
        [Description("Operation type: info, create, list")] string operation,
        [Description("Folder name (for create operation)")] string folderName = null)
    {
        return await ExecuteGraphOperationAsync(
            kernel,
            async (graphClient, userName) =>
            {
                var drive = await graphClient.Me.Drive.GetAsync();

                if (drive == null)
                {
                    return $"Could not access OneDrive for {userName}.";
                }

                return operation.ToLower() switch
                {
                    "info" => CreateSuccessResponse(
                        "OneDrive operations info",
                        userName,
                        ("💾 Drive", drive.Name ?? "OneDrive"),
                        ("📊 Total Space", drive.Quota?.Total.HasValue == true ? $"{drive.Quota.Total.Value / (1024 * 1024 * 1024)} GB" : "Unknown"),
                        ("📈 Used Space", drive.Quota?.Used.HasValue == true ? $"{drive.Quota.Used.Value / (1024 * 1024 * 1024)} GB" : "Unknown")
                    ),

                    "create" => CreateSuccessResponse(
                        "OneDrive folder creation capability",
                        userName,
                        ("📁 Folder", folderName ?? "Not specified"),
                        ("📝 Note", "Folder creation requires advanced Graph Drive API configuration")
                    ),

                    "list" => CreateSuccessResponse(
                        "OneDrive file listing capability",
                        userName,
                        ("📂 Status", "Drive accessible"),
                        ("📝 Note", "File enumeration requires additional SDK work")
                    ),

                    _ => $"Unknown operation '{operation}'. Available: info, create, list"
                };
            },
            "OneDriveFolderOperations"
        );
    }
}