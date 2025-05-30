using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using SemanticKernelPoc.Api.Models;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Memory;

var builder = WebApplication.CreateBuilder(args);

// Add Azure AD authentication with token acquisition support
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("AzureAd"))
    .AddInMemoryTokenCaches();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add Conversation Memory Service
builder.Services.AddSingleton<IConversationMemoryService, InMemoryConversationService>();

// Add Graph Service for plugins
builder.Services.AddSingleton<IGraphService, GraphService>();

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
