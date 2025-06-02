using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.Mail;

public class MailPlugin : BaseGraphPlugin
{
    public MailPlugin(IGraphService graphService, IGraphClientFactory graphClientFactory, ILogger<MailPlugin> logger) 
        : base(graphService, graphClientFactory, logger)
    {
    }

    [KernelFunction, Description("Get recent emails from the user's inbox. Use this when user asks for 'emails', 'mail', 'inbox', 'messages', 'last N emails', 'recent emails', etc. For display purposes, use analysisMode=false. For summary/analysis requests like 'summarize my emails', 'what are my emails about', 'email summary', use analysisMode=true.")]
    public async Task<string> GetRecentEmails(Kernel kernel,
        [Description("Number of recent emails to retrieve (default 5, max 10). Use this when user specifies 'last 2 emails', 'get 5 emails', etc.")] int count = 5,
        [Description("Time period filter: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days like '7' for last 7 days")] string timePeriod = null,
        [Description("Filter by read status: 'unread' for unread only, 'read' for read only, or null for all emails")] string readStatus = null,
        [Description("Filter by importance: 'high', 'normal', 'low', or null for all importance levels")] string importance = null,
        [Description("Analysis mode: ALWAYS set to true when user asks for summaries, analysis, or 'what are my emails about'. Set to false for listing/displaying emails. Keywords that trigger true: summarize, summary, analyze, analysis, what about, content overview.")] bool analysisMode = false)
    {
        try
        {
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            // Build filter conditions
            var filters = new List<string>();
            
            // Time period filter
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue)
                {
                    filters.Add($"receivedDateTime ge {startDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
                if (endDate.HasValue)
                {
                    filters.Add($"receivedDateTime le {endDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
            }

            // Read status filter
            if (!string.IsNullOrWhiteSpace(readStatus))
            {
                if (readStatus.ToLower() == "unread")
                {
                    filters.Add("isRead eq false");
                }
                else if (readStatus.ToLower() == "read")
                {
                    filters.Add("isRead eq true");
                }
            }

            // Importance filter
            if (!string.IsNullOrWhiteSpace(importance))
            {
                var importanceValue = importance.ToLower() switch
                {
                    "high" => "high",
                    "low" => "low",
                    "normal" => "normal",
                    _ => null
                };
                if (importanceValue != null)
                {
                    filters.Add($"importance eq '{importanceValue}'");
                }
            }

            // For analysis mode, get more detailed email content
            var selectFields = analysisMode 
                ? new[] { "subject", "from", "receivedDateTime", "isRead", "importance", "body", "bodyPreview", "id", "webLink" }
                : new[] { "subject", "from", "receivedDateTime", "isRead", "importance", "bodyPreview", "id", "webLink" };

            // Use simplified Messages API with filters
            var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = Math.Min(count, 10);
                requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
                requestConfig.QueryParameters.Select = selectFields;
                
                if (filters.Any())
                {
                    requestConfig.QueryParameters.Filter = string.Join(" and ", filters);
                }
            });

            if (messages?.Value?.Any() == true)
            {
                if (analysisMode)
                {
                    // For analysis mode, use AI to summarize actual email content
                    var emailContents = messages.Value.Select(msg => new
                    {
                        From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                        Subject = msg.Subject ?? "No Subject",
                        ReceivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd") ?? "Unknown",
                        Content = ExtractEmailContent(msg),
                        IsRead = msg.IsRead ?? false,
                        Importance = msg.Importance?.ToString() ?? "Normal"
                    }).ToList();

                    // Create a prompt for AI to summarize the emails
                    var emailSummaryPrompt = $@"Please provide a concise summary of these {emailContents.Count} recent emails for the user. Focus on the main topics, key information, and actionable items. Don't just list metadata - summarize what the emails are actually about:

{string.Join("\n\n", emailContents.Select((email, i) => 
    $"Email {i + 1}:\n" +
    $"From: {email.From}\n" +
    $"Subject: {email.Subject}\n" +
    $"Date: {email.ReceivedDate}\n" +
    $"Content: {email.Content}\n" +
    $"Status: {(email.IsRead ? "Read" : "Unread")}" +
    (email.Importance != "Normal" ? $"\nImportance: {email.Importance}" : "")))}

Provide a helpful summary that tells the user what these emails are about, not just who sent them.";

                    // Use the global kernel to get AI summary
                    var globalKernel = kernel.Services.GetService<Kernel>();
                    if (globalKernel != null)
                    {
                        try
                        {
                            var summaryResult = await globalKernel.InvokePromptAsync(emailSummaryPrompt);
                            return summaryResult.ToString();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to generate AI summary for emails");
                            // Fallback to simple summary
                            return CreateFallbackEmailSummary(emailContents, userName);
                        }
                    }
                    else
                    {
                        return CreateFallbackEmailSummary(emailContents, userName);
                    }
                }
                else
                {
                    // Create email cards for card display mode - must match React EmailItem interface
                    var emailCards = messages.Value.Select((msg, index) => new
                    {
                        id = $"email_{index}_{msg.Id?.GetHashCode().ToString("X")}",
                        subject = msg.Subject?.Length > 80 ? msg.Subject[..80] + "..." : msg.Subject ?? "No Subject",
                        from = msg.From?.EmailAddress?.Name?.Length > 50 ? 
                            msg.From.EmailAddress.Name[..50] + "..." : 
                            msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                        fromEmail = GetValidEmailAddress(msg.From?.EmailAddress?.Address) ?? "",
                        receivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                        receivedDateTime = msg.ReceivedDateTime?.ToString("o"),
                        isRead = msg.IsRead ?? false,
                        importance = msg.Importance?.ToString() ?? "Normal",
                        preview = msg.BodyPreview?.Length > 120 ? msg.BodyPreview[..120] + "..." : msg.BodyPreview ?? "",
                        webLink = msg.WebLink ?? $"https://outlook.office.com/mail/id/{msg.Id}",
                        matchReason = (string)null, // Only used for search results
                        importanceColor = msg.Importance?.ToString()?.ToLower() switch
                        {
                            "high" => "#ef4444",
                            "low" => "#10b981",
                            _ => "#6b7280"
                        },
                        readStatusColor = (msg.IsRead ?? false) ? "#10b981" : "#f59e0b"
                    }).ToList();

                    var functionResponse = $"Found {emailCards.Count} recent emails for {userName}.";

                    // Use the old direct approach temporarily to test
                    kernel.Data["EmailCards"] = emailCards;
                    kernel.Data["HasStructuredData"] = "true";
                    kernel.Data["StructuredDataType"] = "emails";
                    kernel.Data["StructuredDataCount"] = emailCards.Count;
                    kernel.Data["EmailFunctionResponse"] = functionResponse;

                    return functionResponse;
                }
            }

            var filterDesc = BuildFilterDescription(timePeriod, readStatus, importance);
            return $"No emails found for {userName}{(string.IsNullOrEmpty(filterDesc) ? "." : $" with filters: {filterDesc}.")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessing emails for user");
            return $"Error accessing emails: {ex.Message}";
        }
    }

    /// <summary>
    /// Extract meaningful content from an email message
    /// </summary>
    private static string ExtractEmailContent(Message message)
    {
        // Try to get full body content first, fall back to preview
        var content = message.Body?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            content = message.BodyPreview;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return "No content available";
        }

        // Clean up HTML if present and limit length for AI processing
        var cleanContent = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]*>", "");
        cleanContent = System.Net.WebUtility.HtmlDecode(cleanContent);
        
        // Limit to reasonable length for AI processing (about 500 characters per email)
        if (cleanContent.Length > 500)
        {
            cleanContent = cleanContent.Substring(0, 500) + "...";
        }

        return cleanContent.Trim();
    }

    /// <summary>
    /// Create a fallback summary when AI is not available
    /// </summary>
    private static string CreateFallbackEmailSummary(IEnumerable<dynamic> emailContents, string userName)
    {
        var emailList = emailContents.ToList();
        var subjects = emailList.Select(e => e.Subject).Where(s => s != "No Subject").Take(3);
        var senders = emailList.Select(e => e.From).Distinct().Take(3);
        var unreadCount = emailList.Count(e => !e.IsRead);
        var highImportanceCount = emailList.Count(e => e.Importance?.ToString()?.ToLower() == "high");

        return $"Here's a summary of your {emailList.Count} recent emails:\n\n" +
               $"• {unreadCount} unread emails, {highImportanceCount} high importance\n" +
               $"• Recent emails from: {string.Join(", ", senders)}\n" +
               $"• Key subjects: {string.Join(", ", subjects)}\n\n" +
               "The emails cover various topics. Check your inbox for full details.";
    }

    [KernelFunction, Description("Create email draft")]
    public async Task<string> CreateEmailDraft(Kernel kernel,
        [Description("Recipient email address")] string toEmail,
        [Description("Email subject")] string subject,
        [Description("Email body content")] string body,
        [Description("Email importance: low, normal, or high")] string importance = "normal")
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to create email - user authentication required.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = body
                },
                ToRecipients =
                [
                    new() {
                        EmailAddress = new EmailAddress
                        {
                            Address = toEmail
                        }
                    }
                ],
                // Set importance
                Importance = importance.ToLower() switch
                {
                    "high" => Microsoft.Graph.Models.Importance.High,
                    "low" => Microsoft.Graph.Models.Importance.Low,
                    _ => Microsoft.Graph.Models.Importance.Normal
                }
            };

            // Create draft
            await graphClient.Me.Messages.PostAsync(message);

            return $"Successfully created email draft for {userName}:\n" +
                   $"To: {toEmail}\n" +
                   $"Subject: {subject}\n" +
                   $"Importance: {importance}\n" +
                   "Note: Email saved as draft in Outlook.";
        }
        catch (Exception ex)
        {
            return $"Error creating email draft: {ex.Message}";
        }
    }

    [KernelFunction, Description("Send an email immediately")]
    public async Task<string> SendEmail(Kernel kernel,
        [Description("Recipient email address")] string toEmail,
        [Description("Email subject")] string subject,
        [Description("Email body content")] string body,
        [Description("Additional recipients (CC), comma-separated (optional)")] string ccEmails = null,
        [Description("Email importance: low, normal, or high")] string importance = "normal")
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to send email - user authentication required.";
            }

            // Validate email address
            if (string.IsNullOrWhiteSpace(toEmail) || !IsValidEmail(toEmail))
            {
                return $"Invalid recipient email address: '{toEmail}'. Please provide a valid email address.";
            }

            // Validate subject and body
            if (string.IsNullOrWhiteSpace(subject))
            {
                return "Email subject cannot be empty. Please provide a subject for the email.";
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return "Email body cannot be empty. Please provide content for the email.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            // First, test if we can access the mail service
            try
            {
                await graphClient.Me.GetAsync();
            }
            catch (Exception accessEx)
            {
                return $"Unable to access Microsoft Graph - authentication may have expired: {accessEx.Message}";
            }

            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = body
                },
                ToRecipients =
                [
                    new() {
                        EmailAddress = new EmailAddress
                        {
                            Address = toEmail.Trim(),
                            Name = toEmail.Trim()
                        }
                    }
                ]
            };

            // Add CC recipients if provided
            if (!string.IsNullOrWhiteSpace(ccEmails))
            {
                var ccAddresses = ccEmails.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(email => email.Trim())
                    .Where(email => IsValidEmail(email))
                    .Select(email => new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = email,
                            Name = email
                        }
                    }).ToList();

                if (ccAddresses.Any())
                {
                    message.CcRecipients = ccAddresses;
                }
            }

            // Set importance
            message.Importance = importance.ToLower() switch
            {
                "high" => Microsoft.Graph.Models.Importance.High,
                "low" => Microsoft.Graph.Models.Importance.Low,
                _ => Microsoft.Graph.Models.Importance.Normal
            };

            // Send the email using the sendMail endpoint
            var sendMailRequest = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true
            };

            await graphClient.Me.SendMail.PostAsync(sendMailRequest);

            // Verify the email was sent by checking sent items
            var wasSent = await VerifyEmailSent(graphClient, subject, toEmail);

            var result = $"✅ Email sent successfully for {userName}!\n" +
                        $"📧 To: {toEmail}\n" +
                        $"📝 Subject: {subject}\n" +
                        $"🔗 Importance: {importance}\n";

            if (!string.IsNullOrWhiteSpace(ccEmails))
            {
                result += $"📋 CC: {ccEmails}\n";
            }

            result += wasSent ? "✅ Verified: Email appears in Sent Items" : "⚠️ Note: Could not verify delivery (check Sent Items manually)";

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error sending email: {ex.Message}";
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> VerifyEmailSent(GraphServiceClient graphClient, string subject, string toEmail)
    {
        try
        {
            var sentItems = await graphClient.Me.MailFolders["SentItems"].Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = 10;
                requestConfig.QueryParameters.Orderby = ["sentDateTime desc"];
                requestConfig.QueryParameters.Filter = $"subject eq '{subject.Replace("'", "''")}'";
            });

            return sentItems?.Value?.Any(msg =>
                msg.ToRecipients?.Any(recipient =>
                    recipient.EmailAddress?.Address?.Equals(toEmail, StringComparison.OrdinalIgnoreCase) == true) == true) ?? false;
        }
        catch
        {
            return false; // If we can't verify, assume it worked
        }
    }

    [KernelFunction, Description("Search emails by content, subject, sender, or keywords. Use this when user asks to 'search emails', 'find emails from [person]', 'emails about [topic]', 'emails containing [text]', etc. For display purposes, use analysisMode=false. For analysis of search results like 'summarize emails from John', use analysisMode=true.")]
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
            var (success, errorMessage, graphClient, userName) = await GetAuthenticatedGraphClientAsync(kernel);
            if (!success)
            {
                return errorMessage;
            }

            if (string.IsNullOrWhiteSpace(searchQuery) && string.IsNullOrWhiteSpace(fromSender) && string.IsNullOrWhiteSpace(subjectContains) && string.IsNullOrWhiteSpace(timePeriod))
            {
                return "Please provide a search query, sender, subject, or time period to find emails.";
            }

            // Build filter conditions
            var filters = new List<string>();
            
            // Time period filter
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue)
                {
                    filters.Add($"receivedDateTime ge {startDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
                if (endDate.HasValue)
                {
                    filters.Add($"receivedDateTime le {endDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
            }

            // Read status filter
            if (unreadOnly)
            {
                filters.Add("isRead eq false");
            }

            // Search query filter
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                filters.Add($"contains(subject, '{searchQuery.Replace("'", "''")}')");
            }

            // Sender filter
            if (!string.IsNullOrWhiteSpace(fromSender))
            {
                filters.Add($"from/emailAddress/address eq '{fromSender}'");
            }

            // Subject contains filter
            if (!string.IsNullOrWhiteSpace(subjectContains))
            {
                filters.Add($"contains(subject, '{subjectContains.Replace("'", "''")}')");
            }

            // Use simplified Messages API with filters
            var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = Math.Min(maxResults, 10);
                requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
                requestConfig.QueryParameters.Select = ["subject", "from", "receivedDateTime", "isRead", "importance", "bodyPreview"];
                
                if (filters.Any())
                {
                    requestConfig.QueryParameters.Filter = string.Join(" and ", filters);
                }
            });

            var searchTerm = searchQuery ?? fromSender ?? subjectContains ?? "search criteria";

            if (messages?.Value?.Any() == true)
            {
                if (analysisMode)
                {
                    // For analysis mode, use AI to summarize actual email content
                    var emailContents = messages.Value.Select(msg => new
                    {
                        From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                        Subject = msg.Subject ?? "No Subject",
                        ReceivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd") ?? "Unknown",
                        Content = ExtractEmailContent(msg),
                        IsRead = msg.IsRead ?? false,
                        Importance = msg.Importance?.ToString() ?? "Normal",
                        MatchReason = !string.IsNullOrWhiteSpace(searchQuery) ? "Content/Subject" : 
                                     !string.IsNullOrWhiteSpace(fromSender) ? "Sender" : 
                                     !string.IsNullOrWhiteSpace(subjectContains) ? "Subject" : "Search"
                    }).ToList();

                    // Create a prompt for AI to summarize the search results
                    var emailSummaryPrompt = $@"Please provide a concise summary of these {emailContents.Count} emails that matched the search for '{searchTerm}'. Focus on the main topics, key information, and actionable items found in the search results:

{string.Join("\n\n", emailContents.Select((email, i) => 
    $"Email {i + 1}:\n" +
    $"From: {email.From}\n" +
    $"Subject: {email.Subject}\n" +
    $"Date: {email.ReceivedDate}\n" +
    $"Content: {email.Content}\n" +
    $"Match: {email.MatchReason}\n" +
    $"Status: {(email.IsRead ? "Read" : "Unread")}" +
    (email.Importance != "Normal" ? $"\nImportance: {email.Importance}" : "")))}

Provide a helpful summary that tells the user what these matching emails are about and why they matched the search.";

                    // Use the global kernel to get AI summary
                    var globalKernel = kernel.Services.GetService<Kernel>();
                    if (globalKernel != null)
                    {
                        try
                        {
                            var summaryResult = await globalKernel.InvokePromptAsync(emailSummaryPrompt);
                            return summaryResult.ToString();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to generate AI summary for email search results");
                            // Fallback to simple summary
                            return CreateFallbackEmailSummary(emailContents.Cast<dynamic>(), userName);
                        }
                    }
                    else
                    {
                        return CreateFallbackEmailSummary(emailContents.Cast<dynamic>(), userName);
                    }
                }
                else
                {
                    // Create email cards for card display mode - must match React EmailItem interface
                    var emailCards = messages.Value.Select((msg, index) => new
                    {
                        id = $"search_{index}_{msg.Id?.GetHashCode().ToString("X")}",
                        subject = msg.Subject?.Length > 80 ? msg.Subject[..80] + "..." : msg.Subject ?? "No Subject",
                        from = msg.From?.EmailAddress?.Name?.Length > 50 ? 
                            msg.From.EmailAddress.Name[..50] + "..." : 
                            msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                        fromEmail = GetValidEmailAddress(msg.From?.EmailAddress?.Address) ?? "",
                        receivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                        receivedDateTime = msg.ReceivedDateTime?.ToString("o"),
                        isRead = msg.IsRead ?? false,
                        importance = msg.Importance?.ToString() ?? "Normal",
                        preview = msg.BodyPreview?.Length > 120 ? msg.BodyPreview[..120] + "..." : msg.BodyPreview ?? "",
                        webLink = msg.WebLink ?? $"https://outlook.office.com/mail/id/{msg.Id}",
                        matchReason = !string.IsNullOrWhiteSpace(searchQuery) ? "Content/Subject" : 
                                     !string.IsNullOrWhiteSpace(fromSender) ? "Sender" : 
                                     !string.IsNullOrWhiteSpace(subjectContains) ? "Subject" : "Search",
                        importanceColor = msg.Importance?.ToString()?.ToLower() switch
                        {
                            "high" => "#ef4444",
                            "low" => "#10b981",
                            _ => "#6b7280"
                        },
                        readStatusColor = (msg.IsRead ?? false) ? "#10b981" : "#f59e0b"
                    }).ToList();

                    var functionResponse = $"Found {emailCards.Count} emails matching '{searchTerm}' for {userName}.";

                    // Use the old direct approach temporarily to test
                    kernel.Data["EmailCards"] = emailCards;
                    kernel.Data["HasStructuredData"] = "true";
                    kernel.Data["StructuredDataType"] = "emails";
                    kernel.Data["StructuredDataCount"] = emailCards.Count;
                    kernel.Data["EmailFunctionResponse"] = functionResponse;

                    return functionResponse;
                }
            }

            return $"No emails found matching '{searchTerm}' for {userName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching emails for user");
            return $"Error searching emails: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get email statistics")]
    public async Task<string> GetEmailStats(Kernel kernel)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to get email statistics - user authentication required.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            // Get unread count from messages
            var unreadMessages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Filter = "isRead eq false";
                requestConfig.QueryParameters.Count = true;
                requestConfig.QueryParameters.Top = 1;
            });

            // Get total recent count
            var allMessages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = 100;
                requestConfig.QueryParameters.Count = true;
            });

            var stats = new
            {
                UnreadCount = unreadMessages?.OdataCount ?? 0,
                TotalRecentCount = allMessages?.Value?.Count ?? 0,
                LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };

            return $"Email statistics for {userName}:\n" +
                   JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error getting email statistics: {ex.Message}";
        }
    }

    [KernelFunction, Description("Check email sending permissions and access")]
    public async Task<string> CheckEmailPermissions(Kernel kernel)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to check email permissions - user authentication required.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            var results = new List<string>();

            // Test 1: Can we access user profile?
            try
            {
                var user = await graphClient.Me.GetAsync();
                results.Add($"✅ User Access: {user?.DisplayName} ({user?.Mail ?? user?.UserPrincipalName})");
            }
            catch (Exception ex)
            {
                results.Add($"❌ User Access Failed: {ex.Message}");
                return string.Join("\n", results);
            }

            // Test 2: Can we read messages?
            try
            {
                var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Top = 1;
                });
                results.Add($"✅ Mail Read Access: Can read messages ({messages?.Value?.Count ?? 0} test messages)");
            }
            catch (Exception ex)
            {
                results.Add($"❌ Mail Read Access Failed: {ex.Message}");
            }

            // Test 3: Can we access sent items?
            try
            {
                var sentItems = await graphClient.Me.MailFolders["SentItems"].Messages.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Top = 1;
                });
                results.Add($"✅ Sent Items Access: Can access sent folder");
            }
            catch (Exception ex)
            {
                results.Add($"❌ Sent Items Access Failed: {ex.Message}");
            }

            // Test 4: Can we create a draft?
            try
            {
                var testDraft = new Message
                {
                    Subject = "Test Draft - DELETE ME",
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Text,
                        Content = "This is a test draft created to check permissions. Please delete."
                    },
                    ToRecipients =
                    [
                        new() {
                            EmailAddress = new EmailAddress
                            {
                                Address = "test@example.com"
                            }
                        }
                    ]
                };

                var draft = await graphClient.Me.Messages.PostAsync(testDraft);
                if (draft?.Id != null)
                {
                    results.Add($"✅ Draft Creation: Can create drafts (created draft ID: {draft.Id})");

                    // Clean up the test draft
                    try
                    {
                        await graphClient.Me.Messages[draft.Id].DeleteAsync();
                        results.Add($"✅ Draft Cleanup: Successfully deleted test draft");
                    }
                    catch
                    {
                        results.Add($"⚠️ Draft Cleanup: Test draft {draft.Id} needs manual deletion");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Draft Creation Failed: {ex.Message}");
            }

            // Test 5: Check if we can determine send permissions
            results.Add("\n📊 PERMISSION ANALYSIS:");

            if (results.Any(r => r.Contains("Mail Read Access") && r.Contains('✅')))
            {
                results.Add("✅ Mail.Read permission: GRANTED");
            }
            else
            {
                results.Add("❌ Mail.Read permission: DENIED or INSUFFICIENT");
            }

            if (results.Any(r => r.Contains("Draft Creation") && r.Contains('✅')))
            {
                results.Add("✅ Mail.ReadWrite permission: LIKELY GRANTED");
                results.Add("⚠️ Mail.Send permission: UNKNOWN (requires actual send test)");
            }
            else
            {
                results.Add("❌ Mail.ReadWrite permission: DENIED or INSUFFICIENT");
                results.Add("❌ Mail.Send permission: LIKELY DENIED");
            }

            results.Add("\n📝 RECOMMENDATIONS:");
            results.Add("• If email sending fails, check Azure AD app registration permissions");
            results.Add("• Ensure Mail.Send delegated permission is granted and admin-consented");
            results.Add("• Verify organization policies allow external email sending");
            results.Add("• Check if multi-factor authentication or conditional access is blocking API calls");

            return $"Email Permissions Check for {userName}:\n\n" + string.Join("\n", results);
        }
        catch (Exception ex)
        {
            return $"Error checking email permissions: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get emails from a specific sender or person. Use this when user asks 'emails from John', 'messages from my boss', 'emails from john@company.com', etc.")]
    public async Task<string> GetEmailsFromSender(Kernel kernel,
        [Description("Sender's email address or name to search for")] string senderEmailOrName,
        [Description("Number of emails to retrieve from this sender (default 5, max 10)")] int count = 5,
        [Description("Time period: 'today', 'yesterday', 'this_week', 'last_week', 'this_month', 'last_month', or number of days")] string timePeriod = null,
        [Description("Include only unread emails from this sender")] bool unreadOnly = false)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to search emails - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(senderEmailOrName))
            {
                return "Please provide a sender email address or name to search for.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            // Build filter conditions
            var filters = new List<string>();
            
            // Sender filter - try both email address and display name
            if (senderEmailOrName.Contains("@"))
            {
                filters.Add($"from/emailAddress/address eq '{senderEmailOrName}'");
            }
            else
            {
                filters.Add($"contains(from/emailAddress/name, '{senderEmailOrName.Replace("'", "''")}')");
            }

            // Time period filter
            if (!string.IsNullOrWhiteSpace(timePeriod))
            {
                var (startDate, endDate) = ParseTimePeriod(timePeriod);
                if (startDate.HasValue)
                {
                    filters.Add($"receivedDateTime ge {startDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
                if (endDate.HasValue)
                {
                    filters.Add($"receivedDateTime le {endDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }
            }

            // Read status filter
            if (unreadOnly)
            {
                filters.Add("isRead eq false");
            }

            var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = Math.Min(count, 10);
                requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
                requestConfig.QueryParameters.Select = ["subject", "from", "receivedDateTime", "isRead", "importance", "bodyPreview", "id", "webLink"];
                requestConfig.QueryParameters.Filter = string.Join(" and ", filters);
            });

            if (messages?.Value?.Any() == true)
            {
                // Create email cards
                var emailCards = messages.Value.Select((msg, index) => new
                {
                    id = $"sender_{index}_{msg.Id?.GetHashCode().ToString("X")}",
                    subject = msg.Subject?.Length > 80 ? msg.Subject[..80] + "..." : msg.Subject ?? "No Subject",
                    from = msg.From?.EmailAddress?.Name?.Length > 50 ? 
                        msg.From.EmailAddress.Name[..50] + "..." : 
                        msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                    fromEmail = GetValidEmailAddress(msg.From?.EmailAddress?.Address) ?? "",
                    receivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                    receivedDateTime = msg.ReceivedDateTime?.ToString("o"),
                    isRead = msg.IsRead ?? false,
                    importance = msg.Importance?.ToString() ?? "Normal",
                    preview = msg.BodyPreview?.Length > 120 ? msg.BodyPreview[..120] + "..." : msg.BodyPreview ?? "",
                    webLink = msg.WebLink ?? $"https://outlook.office.com/mail/id/{msg.Id}",
                    matchReason = (string)null, // Only used for search results
                    importanceColor = msg.Importance?.ToString()?.ToLower() switch
                    {
                        "high" => "#ef4444",
                        "low" => "#10b981",
                        _ => "#6b7280"
                    },
                    readStatusColor = (msg.IsRead ?? false) ? "#10b981" : "#f59e0b"
                }).ToList();

                // Store structured data in kernel data for the system to process
                kernel.Data["EmailCards"] = emailCards;
                kernel.Data["HasStructuredData"] = "true";
                kernel.Data["StructuredDataType"] = "emails";
                kernel.Data["StructuredDataCount"] = emailCards.Count;
                kernel.Data["EmailFunctionResponse"] = $"Found {emailCards.Count} emails from '{senderEmailOrName}' for {userName}.";

                return $"Found {emailCards.Count} emails from '{senderEmailOrName}' for {userName}.";
            }

            var filterDesc = BuildFilterDescription(timePeriod, unreadOnly ? "unread" : null, null);
            return $"No emails found from '{senderEmailOrName}' for {userName}{(string.IsNullOrEmpty(filterDesc) ? "." : $" with filters: {filterDesc}.")}";
        }
        catch (Exception ex)
        {
            return $"Error searching emails from sender: {ex.Message}";
        }
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

    private static string BuildFilterDescription(string timePeriod, string readStatus, string importance)
    {
        var filters = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(timePeriod))
            filters.Add($"time: {timePeriod}");
        
        if (!string.IsNullOrWhiteSpace(readStatus))
            filters.Add($"status: {readStatus}");
        
        if (!string.IsNullOrWhiteSpace(importance))
            filters.Add($"importance: {importance}");
        
        return string.Join(", ", filters);
    }

    private static string GetValidEmailAddress(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return null;

        try
        {
            var addr = new System.Net.Mail.MailAddress(emailAddress);
            return addr.Address == emailAddress ? emailAddress : null;
        }
        catch
        {
            return null;
        }
    }
}