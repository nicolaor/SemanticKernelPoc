using Microsoft.Graph;
using Microsoft.SemanticKernel;

namespace SemanticKernelPoc.Api.Services.Graph;

public interface IGraphService
{
    /// <summary>
    /// Create a GraphServiceClient with user authentication using On-Behalf-Of flow
    /// </summary>
    Task<GraphServiceClient> CreateClientAsync(string userAccessToken);
    
    /// <summary>
    /// Create a GraphServiceClient with user authentication (legacy synchronous method)
    /// </summary>
    GraphServiceClient CreateClient(string userAccessToken);
    
    /// <summary>
    /// Extract user context from kernel data
    /// </summary>
    (string AccessToken, string UserName, string UserId) GetUserContext(Kernel kernel);
    
    /// <summary>
    /// Validate user authentication and return context
    /// </summary>
    Task<(bool IsValid, string ErrorMessage, GraphServiceClient Client, string UserName)> ValidateUserAccessAsync(Kernel kernel);
} 