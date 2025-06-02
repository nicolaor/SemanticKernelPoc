using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;
using SemanticKernelPoc.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Azure AD authentication (without token acquisition - we'll handle OBO manually)
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");

// Explicitly configure JwtBearerOptions for audience validation
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var audienceConfig = builder.Configuration.GetSection("AzureAd:Audience");
    if (audienceConfig.Exists())
    {
        var audiences = audienceConfig.Get<string[]>();
        if (audiences != null && audiences.Length > 0)
        {
            options.TokenValidationParameters.ValidAudiences = audiences;
            options.TokenValidationParameters.ValidateAudience = true; // Ensure audience validation is active
        }
        else
        {
            var singleAudience = audienceConfig.Get<string>();
            if (!string.IsNullOrEmpty(singleAudience))
            {
                options.TokenValidationParameters.ValidAudience = singleAudience;
                options.TokenValidationParameters.ValidateAudience = true; // Ensure audience validation is active
            }
        }
    }
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add conversation memory service
builder.Services.AddSingleton<IConversationMemoryService, InMemoryConversationService>();

// Add response processing service for structured responses
builder.Services.AddScoped<IResponseProcessingService, ResponseProcessingService>();

// Add intent detection service for structured output
builder.Services.AddScoped<IIntentDetectionService, IntentDetectionService>();

// Add Graph Service for plugins (now using manual OBO approach)
builder.Services.AddScoped<IGraphService, GraphService>();

// Add HttpClient for MCP communication
builder.Services.AddHttpClient<IMcpClientService, McpClientService>(client =>
{
    // Configure HttpClient for MCP server communication
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Accept self-signed certificates in development
    if (builder.Environment.IsDevelopment())
    {
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;
    }
    
    return handler;
});

// Add MCP Client Service
builder.Services.AddScoped<IMcpClientService, McpClientService>();

// Semantic Kernel configuration (global - for AI service only)
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("SemanticKernel").Get<SemanticKernelConfig>()
        ?? throw new InvalidOperationException("SemanticKernel configuration is missing");

    var kernelBuilder = Kernel.CreateBuilder();

    if (config.UseAzureOpenAI)
    {
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: config.DeploymentOrModelId,
            endpoint: config.Endpoint,
            apiKey: config.ApiKey);
    }
    else
    {
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: config.DeploymentOrModelId,
            apiKey: config.ApiKey);
    }

    return kernelBuilder.Build();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure JSON serialization to use camelCase
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Configure Swagger with Azure AD authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SemanticKernelPoc API", Version = "v1" });

    var clientId = builder.Configuration["AzureAd:ClientId"];
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { $"api://{clientId}/access_as_user", "Access the API as a user" }
                }
            }
        }
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            new[] { $"api://{clientId}/access_as_user" }
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SemanticKernelPoc API v1");
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
        c.OAuthUsePkce();
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
