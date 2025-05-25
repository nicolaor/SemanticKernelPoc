namespace SemanticKernelPoc.Api.Models;

public class SemanticKernelConfig
{
    public string DeploymentOrModelId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool UseAzureOpenAI { get; set; } = true;
} 