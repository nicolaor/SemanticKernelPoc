using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPoc.Api.Services.Graph;

namespace SemanticKernelPoc.Api.Plugins.Mail;

public class MailPlugin(IGraphService graphService, ILogger<MailPlugin> logger) : BaseGraphPlugin(graphService, logger)
{
    [KernelFunction, Description("Get recent emails (simplified)")]
    public async Task<string> GetRecentEmails(Kernel kernel,
        [Description("Number of recent emails to retrieve (default 10)")] int count = 10)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to access emails - user authentication required.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            // Use simplified Messages API
            var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Top = Math.Min(count, 10);
                requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
                requestConfig.QueryParameters.Select = ["subject", "from", "receivedDateTime", "isRead", "importance", "bodyPreview"];
            });

            if (messages?.Value?.Any() == true)
            {
                var emailList = messages.Value.Select(msg => new
                {
                    msg.Subject,
                    From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                    ReceivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                    IsRead = msg.IsRead ?? false,
                    Importance = msg.Importance?.ToString() ?? "Normal",
                    Preview = msg.BodyPreview?.Length > 100 ? msg.BodyPreview[..100] + "..." : msg.BodyPreview
                });

                return $"Recent emails for {userName}:\n" +
                       JsonSerializer.Serialize(emailList, new JsonSerializerOptions { WriteIndented = true });
            }

            return $"No recent emails found for {userName}.";
        }
        catch (Exception ex)
        {
            return $"Error accessing emails: {ex.Message}";
        }
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
                    recipient.EmailAddress?.Address?.Equals(toEmail, StringComparison.OrdinalIgnoreCase) == true) == true) == true;
        }
        catch
        {
            return false; // If we can't verify, assume it worked
        }
    }

    [KernelFunction, Description("Search emails (basic)")]
    public async Task<string> SearchEmails(Kernel kernel,
        [Description("Search query")] string searchQuery,
        [Description("Maximum number of results (default 5)")] int maxResults = 5)
    {
        try
        {
            var userAccessToken = kernel.Data.TryGetValue("UserAccessToken", out var token) ? token?.ToString() : null;
            var userName = kernel.Data.TryGetValue("UserName", out var name) ? name?.ToString() : "User";

            if (string.IsNullOrEmpty(userAccessToken))
            {
                return "Unable to search emails - user authentication required.";
            }

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return "Please provide a search query to find emails.";
            }

            var graphClient = await CreateClientAsync(userAccessToken);

            // Search using the search API
            var messages = await graphClient.Me.Messages.GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Search = $"\"{searchQuery}\"";
                requestConfig.QueryParameters.Top = Math.Min(maxResults, 10);
                requestConfig.QueryParameters.Orderby = ["receivedDateTime desc"];
                requestConfig.QueryParameters.Select = ["subject", "from", "receivedDateTime", "isRead", "importance", "bodyPreview"];
            });

            if (messages?.Value?.Any() == true)
            {
                var emailList = messages.Value.Select(msg => new
                {
                    msg.Subject,
                    From = msg.From?.EmailAddress?.Name ?? msg.From?.EmailAddress?.Address ?? "Unknown",
                    ReceivedDate = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                    IsRead = msg.IsRead ?? false,
                    Importance = msg.Importance?.ToString() ?? "Normal",
                    Preview = msg.BodyPreview?.Length > 100 ? msg.BodyPreview[..100] + "..." : msg.BodyPreview,
                    MatchReason = "Content or Subject"
                });

                return $"Email search results for '{searchQuery}' (found {emailList.Count()} matches for {userName}):\n" +
                       JsonSerializer.Serialize(emailList, new JsonSerializerOptions { WriteIndented = true });
            }

            return $"No emails found matching '{searchQuery}' for {userName}.";
        }
        catch (Exception ex)
        {
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

    private async Task<GraphServiceClient> CreateClientAsync(string userAccessToken)
    {
        // Use the injected GraphService to create client with On-Behalf-Of flow
        return await _graphService.CreateClientAsync(userAccessToken);
    }
}