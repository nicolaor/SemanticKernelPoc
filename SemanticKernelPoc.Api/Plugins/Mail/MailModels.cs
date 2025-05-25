namespace SemanticKernelPoc.Api.Plugins.Mail;

public record MailStatistics(
    int UnreadCount,
    int TotalRecentCount,
    string LastChecked
);

public record EmailValidationResult(
    bool IsValid,
    string ErrorMessage = null
);

public record EmailSearchResult(
    string Subject,
    string From,
    string ReceivedDate,
    bool IsRead,
    string Preview,
    string Importance = null
); 