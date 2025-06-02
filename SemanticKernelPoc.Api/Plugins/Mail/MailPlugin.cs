using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;
using SemanticKernelPoc.Api.Services.Shared;
using SharedConstants = SemanticKernelPoc.Api.Services.Shared.Constants;

namespace SemanticKernelPoc.Api.Plugins.Mail;

public class MailPlugin : BaseGraphPlugin
{
    private readonly ICardBuilderService _cardBuilder;
    private readonly IAnalysisModeService _analysisMode;
    private readonly ITextProcessingService _textProcessor;

    public MailPlugin(
        IGraphService graphService, 
        IGraphClientFactory graphClientFactory, 
        ILogger<MailPlugin> logger,
        ICardBuilderService cardBuilder,
        IAnalysisModeService analysisMode,
        ITextProcessingService textProcessor) 
        : base(graphService, graphClientFactory, logger)
    {
        _cardBuilder = cardBuilder;
        _analysisMode = analysisMode;
        _textProcessor = textProcessor;
    }

    [KernelFunction, Description("Get recent emails from the user's inbox. Use this when user asks for 'emails', 'mail', 'inbox', 'messages', 'last N emails', 'recent emails', etc. For display purposes, use analysisMode=false. For summary/analysis requests like 'summarize my emails', 'what are my emails about', 'email summary', use analysisMode=true. Keywords that trigger analysis mode: summarize, summary, analyze, analysis, what about, content overview.")]
    public async Task<string> GetRecentEmails(Kernel kernel,
        [Description("Number of recent emails to retrieve (default 5, max 10). Use this when user specifies 'last 2 emails', 'get 5 emails', etc.")] int count = 5,
        [Description("Time period filter: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days like '7' for last 7 days")] string timePeriod = null,
        [Description("Filter by read status: 'unread' for unread only, 'read' for read only, or null for all emails")] string readStatus = null,
        [Description("Filter by importance: 'high', 'normal', 'low', or null for all importance levels")] string importance = null,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what are my emails about'. Set to false for listing/displaying emails. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        try
        {
            // Step 1: Authenticate and get client
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            // Step 2: Retrieve emails with filters
            var messages = await RetrieveRecentEmailsAsync(graphClient, count, timePeriod, readStatus, importance, analysisMode);

            // Step 3: Generate response based on mode and results
            return await GenerateEmailResponseAsync(kernel, messages, userName, analysisMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving emails");
            return $"❌ Error retrieving emails: {ex.Message}";
        }
    }

    #region GetRecentEmails Helper Methods

    private async Task<IList<Message>?> RetrieveRecentEmailsAsync(
        GraphServiceClient graphClient, 
        int count, 
        string timePeriod, 
        string readStatus, 
        string importance, 
        bool analysisMode)
    {
        var filters = BuildEmailFilters(timePeriod, readStatus, importance);
        var selectFields = GetEmailSelectFields(analysisMode);

        var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Top = Math.Min(count, SharedConstants.QueryLimits.MaxEmailCount);
            requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
            requestConfig.QueryParameters.Select = selectFields;
            
            if (filters.Any())
            {
                requestConfig.QueryParameters.Filter = string.Join(" and ", filters);
            }
        });

        return messages?.Value;
    }

    private async Task<string> GenerateEmailResponseAsync(
        Kernel kernel, 
        IList<Message>? messages, 
        string userName, 
        bool analysisMode)
    {
        if (messages?.Any() != true)
        {
            return $"No emails found for {userName} with the specified criteria.";
        }

        return analysisMode 
            ? await GenerateEmailAnalysisResponse(kernel, messages, userName)
            : GenerateEmailCardResponse(kernel, messages, userName);
    }

    private async Task<string> GenerateEmailAnalysisResponse(Kernel kernel, IList<Message> messages, string userName)
    {
        return await _analysisMode.GenerateAISummaryAsync(
            kernel,
            messages,
            "emails",
            userName,
            CreateEmailAnalysisTransformer());
    }

    private string GenerateEmailCardResponse(Kernel kernel, IList<Message> messages, string userName)
    {
        var emailCards = _cardBuilder.BuildEmailCards(messages, (msg, index) => CreateEmailCard(msg, index));
        var functionResponse = $"Found {emailCards.Count} recent emails for {userName}.";

        _cardBuilder.SetCardData(kernel, "emails", emailCards, emailCards.Count, functionResponse);
        return functionResponse;
    }

    private string[] GetEmailSelectFields(bool analysisMode)
    {
        return analysisMode 
            ? SharedConstants.GraphSelectFields.EmailWithBody
            : SharedConstants.GraphSelectFields.EmailBasic;
    }

    private Func<Message, object> CreateEmailAnalysisTransformer()
    {
        return msg => new
        {
            From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? SharedConstants.DefaultText.Unknown,
            Subject = msg.Subject ?? SharedConstants.DefaultText.NoSubject,
            ReceivedDate = msg.ReceivedDateTime?.ToString(SharedConstants.DateFormats.StandardDate) ?? SharedConstants.DefaultText.Unknown,
            Content = _textProcessor.CleanAndLimitContent(msg.Body?.Content ?? msg.BodyPreview),
            IsRead = msg.IsRead ?? false,
            Importance = msg.Importance?.ToString() ?? SharedConstants.DefaultText.Normal
        };
    }

    #endregion

    [KernelFunction, Description("Create email draft")]
    public async Task<string> CreateEmailDraft(Kernel kernel,
        [Description("Recipient email address")] string toEmail,
        [Description("Email subject")] string subject,
        [Description("Email body content")] string body,
        [Description("Email importance: low, normal, or high")] string importance = "normal")
    {
        try
        {
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            var validationError = ValidateEmailParameters(toEmail, subject, body);
            if (validationError != null)
            {
                return validationError;
            }

            var draft = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = body
                },
                ToRecipients = new List<Recipient>
                {
                    new() {
                        EmailAddress = new EmailAddress
                        {
                            Address = toEmail.Trim()
                        }
                    }
                },
                Importance = ParseImportance(importance)
            };

            var createdDraft = await graphClient.Me.Messages.PostAsync(draft);

            return CreateSuccessResponse("Email draft created", userName,
                ("📧 To", toEmail),
                ("📝 Subject", subject),
                ("🔗 Priority", importance),
                ("🌐 View", SharedConstants.ServiceUrls.GetOutlookMailUrl(createdDraft?.Id ?? "")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email draft");
            return $"❌ Error creating email draft: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search emails by content, subject, sender, or keywords. Use this when user asks to 'search emails', 'find emails from [person]', 'emails about [topic]', 'emails containing [text]', etc. For display purposes, use analysisMode=false. For analysis of search results like 'summarize emails from John', use analysisMode=true. Keywords that trigger analysis mode: summarize, summary, analyze, analysis, what about, content overview.")]
    public async Task<string> SearchEmails(Kernel kernel,
        [Description("Search query to find in email content, subject, or sender. Can be keywords, phrases, or specific text")] string searchQuery = null,
        [Description("Search specifically by sender name or email address. Use when user asks 'emails from John' or 'emails from john@company.com'")] string fromSender = null,
        [Description("Search specifically in email subject line. Use when user asks 'emails with subject containing [text]'")] string subjectContains = null,
        [Description("Time period to search within: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days")] string timePeriod = null,
        [Description("Maximum number of results (default 5, max 10)")] int maxResults = 5,
        [Description("Include only unread emails in search results")] bool unreadOnly = false,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries or analysis of search results. Set to false for listing/displaying search results. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        try
        {
            // Step 1: Authenticate and validate
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            var validationError = ValidateSearchParameters(searchQuery, fromSender, subjectContains);
            if (validationError != null)
            {
                return validationError;
            }

            // Step 2: Execute search
            var messages = await ExecuteEmailSearchAsync(graphClient, searchQuery, fromSender, subjectContains, timePeriod, maxResults, unreadOnly, analysisMode);

            // Step 3: Generate response based on mode and results
            return await GenerateSearchResponseAsync(kernel, messages, userName, searchQuery, fromSender, subjectContains, analysisMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching emails");
            return $"❌ Error searching emails: {ex.Message}";
        }
    }

    #region SearchEmails Helper Methods

    private string? ValidateSearchParameters(string searchQuery, string fromSender, string subjectContains)
    {
        return IsSearchParametersEmpty(searchQuery, fromSender, subjectContains)
            ? "Please provide at least one search parameter: searchQuery, fromSender, or subjectContains."
            : null;
    }

    private async Task<IList<Message>?> ExecuteEmailSearchAsync(
        GraphServiceClient graphClient,
        string searchQuery,
        string fromSender,
        string subjectContains,
        string timePeriod,
        int maxResults,
        bool unreadOnly,
        bool analysisMode)
    {
        var filters = BuildSearchFilters(searchQuery, fromSender, subjectContains, timePeriod, unreadOnly);
        var selectFields = GetEmailSelectFields(analysisMode);

        var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Top = Math.Min(maxResults, SharedConstants.QueryLimits.MaxEmailCount);
            requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
            requestConfig.QueryParameters.Select = selectFields;
            
            if (filters.Any())
            {
                requestConfig.QueryParameters.Filter = string.Join(" and ", filters);
            }
        });

        return messages?.Value;
    }

    private async Task<string> GenerateSearchResponseAsync(
        Kernel kernel,
        IList<Message>? messages,
        string userName,
        string searchQuery,
        string fromSender,
        string subjectContains,
        bool analysisMode)
    {
        if (messages?.Any() != true)
        {
            return $"No emails found matching your search criteria for {userName}.";
        }

        return analysisMode
            ? await GenerateSearchAnalysisResponse(kernel, messages, userName, searchQuery, fromSender, subjectContains)
            : GenerateSearchCardResponse(kernel, messages, userName, searchQuery, fromSender, subjectContains);
    }

    private async Task<string> GenerateSearchAnalysisResponse(
        Kernel kernel,
        IList<Message> messages,
        string userName,
        string searchQuery,
        string fromSender,
        string subjectContains)
    {
        var transformer = CreateSearchAnalysisTransformer(searchQuery, fromSender, subjectContains);
        
        return await _analysisMode.GenerateAISummaryAsync(
            kernel,
            messages,
            "emails",
            userName,
            transformer);
    }

    private string GenerateSearchCardResponse(
        Kernel kernel,
        IList<Message> messages,
        string userName,
        string searchQuery,
        string fromSender,
        string subjectContains)
    {
        var emailCards = _cardBuilder.BuildEmailCards(messages, (msg, index) => 
            CreateSearchEmailCard(msg, index, searchQuery, fromSender, subjectContains));
        
        var functionResponse = $"Found {emailCards.Count} emails matching your search for {userName}.";

        _cardBuilder.SetCardData(kernel, "emails", emailCards, emailCards.Count, functionResponse);
        return functionResponse;
    }

    private Func<Message, object> CreateSearchAnalysisTransformer(string searchQuery, string fromSender, string subjectContains)
    {
        return msg => new
        {
            From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? SharedConstants.DefaultText.Unknown,
            Subject = msg.Subject ?? SharedConstants.DefaultText.NoSubject,
            ReceivedDate = msg.ReceivedDateTime?.ToString(SharedConstants.DateFormats.StandardDate) ?? SharedConstants.DefaultText.Unknown,
            Content = _textProcessor.CleanAndLimitContent(msg.Body?.Content ?? msg.BodyPreview),
            MatchReason = DetermineMatchReason(msg, searchQuery, fromSender, subjectContains)
        };
    }

    #endregion

    #region Private Helper Methods

    private List<string> BuildEmailFilters(string timePeriod, string readStatus, string importance)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(readStatus))
        {
            filters.Add(readStatus.ToLower() == "unread" ? "isRead eq false" : "isRead eq true");
        }

        if (!string.IsNullOrWhiteSpace(importance))
        {
            filters.Add($"importance eq '{importance.ToLower()}'");
        }

        if (!string.IsNullOrWhiteSpace(timePeriod))
        {
            var dateFilter = ParseTimePeriodFilter(timePeriod);
            if (dateFilter != null)
            {
                filters.Add(dateFilter);
            }
        }

        return filters;
    }

    private List<string> BuildSearchFilters(string searchQuery, string fromSender, string subjectContains, string timePeriod, bool unreadOnly)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            filters.Add($"(contains(subject,'{searchQuery}') or contains(bodyPreview,'{searchQuery}'))");
        }

        if (!string.IsNullOrWhiteSpace(fromSender))
        {
            filters.Add($"(contains(from/emailAddress/name,'{fromSender}') or contains(from/emailAddress/address,'{fromSender}'))");
        }

        if (!string.IsNullOrWhiteSpace(subjectContains))
        {
            filters.Add($"contains(subject,'{subjectContains}')");
        }

        if (unreadOnly)
        {
            filters.Add("isRead eq false");
        }

        if (!string.IsNullOrWhiteSpace(timePeriod))
        {
            var dateFilter = ParseTimePeriodFilter(timePeriod);
            if (dateFilter != null)
            {
                filters.Add(dateFilter);
            }
        }

        return filters;
    }

    private object CreateEmailCard(Message msg, int index)
    {
        return new
        {
            id = $"email_{index}_{msg.Id?.GetHashCode():X}",
            subject = _textProcessor.TruncateText(msg.Subject, SharedConstants.TextLimits.EmailSubjectMaxLength, SharedConstants.DefaultText.NoSubject),
            from = _textProcessor.GetSafeDisplayName(msg.From?.EmailAddress?.Name, msg.From?.EmailAddress?.Address),
            fromEmail = _textProcessor.GetValidEmailAddress(msg.From?.EmailAddress?.Address),
            receivedDate = msg.ReceivedDateTime?.ToString(SharedConstants.DateFormats.StandardDateTime) ?? SharedConstants.DefaultText.Unknown,
            receivedDateTime = msg.ReceivedDateTime?.ToString(SharedConstants.DateFormats.RoundTripDateTime),
            isRead = msg.IsRead ?? false,
            importance = msg.Importance?.ToString() ?? SharedConstants.DefaultText.Normal,
            preview = _textProcessor.TruncateText(msg.BodyPreview, SharedConstants.TextLimits.EmailPreviewMaxLength),
            webLink = msg.WebLink ?? SharedConstants.ServiceUrls.GetOutlookMailUrl(msg.Id ?? ""),
            matchReason = (string)null, // Only used for search results
            importanceColor = _textProcessor.GetPriorityColor(msg.Importance?.ToString()),
            readStatusColor = _textProcessor.GetReadStatusColor(msg.IsRead ?? false)
        };
    }

    private object CreateSearchEmailCard(Message msg, int index, string searchQuery, string fromSender, string subjectContains)
    {
        var baseCard = CreateEmailCard(msg, index);
        var cardDict = new Dictionary<string, object>();
        
        // Copy all properties from base card
        foreach (var property in baseCard.GetType().GetProperties())
        {
            cardDict[property.Name] = property.GetValue(baseCard);
        }
        
        // Update specific properties for search results
        cardDict["id"] = $"search_{index}_{msg.Id?.GetHashCode():X}";
        cardDict["matchReason"] = DetermineMatchReason(msg, searchQuery, fromSender, subjectContains);
        
        return cardDict;
    }

    private string DetermineMatchReason(Message msg, string searchQuery, string fromSender, string subjectContains)
    {
        if (!string.IsNullOrWhiteSpace(fromSender)) return "Sender";
        if (!string.IsNullOrWhiteSpace(subjectContains)) return "Subject";
        if (!string.IsNullOrWhiteSpace(searchQuery)) return "Content/Subject";
        return "Search";
    }

    private static bool IsSearchParametersEmpty(string searchQuery, string fromSender, string subjectContains)
    {
        return string.IsNullOrWhiteSpace(searchQuery) && 
               string.IsNullOrWhiteSpace(fromSender) && 
               string.IsNullOrWhiteSpace(subjectContains);
    }

    private static string ValidateEmailParameters(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return "To email address is required.";
        if (string.IsNullOrWhiteSpace(subject)) return "Email subject is required.";
        if (string.IsNullOrWhiteSpace(body)) return "Email body is required.";
        if (!toEmail.Contains("@")) return "Invalid email address format.";
        return null;
    }

    private static Importance ParseImportance(string importance)
    {
        return importance?.ToLower() switch
        {
            "high" => Importance.High,
            "low" => Importance.Low,
            _ => Importance.Normal
        };
    }

    private static string ParseTimePeriodFilter(string timePeriod)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        return timePeriod?.ToLower() switch
        {
            "today" => $"receivedDateTime ge {today:yyyy-MM-ddTHH:mm:ssZ}",
            "yesterday" => $"receivedDateTime ge {today.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ} and receivedDateTime lt {today:yyyy-MM-ddTHH:mm:ssZ}",
            "this_week" => $"receivedDateTime ge {today.AddDays(-(int)today.DayOfWeek):yyyy-MM-ddTHH:mm:ssZ}",
            "last_week" => $"receivedDateTime ge {today.AddDays(-7 - (int)today.DayOfWeek):yyyy-MM-ddTHH:mm:ssZ} and receivedDateTime lt {today.AddDays(-(int)today.DayOfWeek):yyyy-MM-ddTHH:mm:ssZ}",
            "this_month" => $"receivedDateTime ge {new DateTime(today.Year, today.Month, 1):yyyy-MM-ddTHH:mm:ssZ}",
            "last_month" => $"receivedDateTime ge {new DateTime(today.Year, today.Month, 1).AddMonths(-1):yyyy-MM-ddTHH:mm:ssZ} and receivedDateTime lt {new DateTime(today.Year, today.Month, 1):yyyy-MM-ddTHH:mm:ssZ}",
            _ when int.TryParse(timePeriod, out var days) && days > 0 => $"receivedDateTime ge {today.AddDays(-days):yyyy-MM-ddTHH:mm:ssZ}",
            _ => null
        };
    }

    #endregion
}