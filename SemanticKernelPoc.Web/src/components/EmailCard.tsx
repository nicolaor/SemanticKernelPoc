import React from "react";

interface EmailItem {
  id: string;
  subject: string;
  from: string;
  fromEmail: string;
  receivedDate: string;
  receivedDateTime?: string;
  isRead: boolean;
  importance: string;
  preview: string;
  webLink?: string;
  matchReason?: string;
  importanceColor: string;
  readStatusColor: string;
}

interface EmailCardProps {
  emails: EmailItem[];
}

// Helper functions moved outside component for better performance
const getImportanceIcon = (importance: string) => {
  switch (importance.toLowerCase()) {
    case "high":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-8.293l-3-3a1 1 0 00-1.414 0l-3 3a1 1 0 001.414 1.414L9 9.414V13a1 1 0 102 0V9.414l1.293 1.293a1 1 0 001.414-1.414z" clipRule="evenodd" />
        </svg>
      );
    case "low":
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-11a1 1 0 10-2 0v3.586L7.707 9.293a1 1 0 00-1.414 1.414l3 3a1 1 0 001.414 0l3-3a1 1 0 00-1.414-1.414L11 10.586V7z" clipRule="evenodd" />
        </svg>
      );
    default:
      return (
        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M3 10a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
        </svg>
      );
  }
};

const getReadStatusIcon = (isRead: boolean) => {
  if (isRead) {
    return (
      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
      </svg>
    );
  } else {
    return (
      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
      </svg>
    );
  }
};

const getEmailColor = (index: number) => {
  const colors = [
    { from: "#0078D4", to: "#106EBE" }, // Outlook Blue
    { from: "#0F6CBD", to: "#005A9E" }, // Dark Blue
    { from: "#40E0D0", to: "#008B8B" }, // Turquoise
    { from: "#4682B4", to: "#2F4F4F" }, // Steel Blue
    { from: "#6495ED", to: "#4169E1" }, // Cornflower Blue
    { from: "#87CEEB", to: "#4682B4" }, // Sky Blue
  ];
  return colors[index % colors.length];
};

// Individual email item component for better performance
const EmailItem: React.FC<{ email: EmailItem; index: number }> = React.memo(({ email, index }) => {
  const emailColor = getEmailColor(index);

  return (
    <div
      key={email.id}
      className="group rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
      style={{
        background: "var(--card-bg)",
        border: "1px solid var(--border-primary)",
      }}
    >
      {/* Email Color Bar */}
      <div
        className="h-1"
        style={{
          background: `linear-gradient(to right, ${emailColor.from}, ${emailColor.to})`,
        }}
        title="Email color indicator - helps distinguish between different emails"
      ></div>

      <div className="p-4">
        {/* Email Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1 min-w-0">
            <h4 className="text-base font-semibold mb-1 transition-colors" style={{ color: "var(--text-primary)", fontWeight: "600" }} title={`Email: ${email.subject}`}>
              {email.subject}
            </h4>

            <div className="flex items-center space-x-2 mb-2">
              <span className="text-sm font-medium" style={{ color: "var(--text-secondary)" }} title={`From: ${email.from}${email.fromEmail ? ` (${email.fromEmail})` : ''}`}>
                From: {email.from}
              </span>
            </div>

            {email.preview && (
              <p className="text-sm mb-2 line-clamp-2" style={{ color: "var(--text-secondary)" }} title={`Email preview: ${email.preview}`}>
                {email.preview}
              </p>
            )}

            <div className="flex items-center space-x-2 flex-wrap gap-y-1">
              {/* Read Status Badge */}
              <span
                className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                style={{
                  backgroundColor: `${email.readStatusColor}20`,
                  color: email.readStatusColor,
                  border: `1px solid ${email.readStatusColor}40`,
                }}
                title={`Email status: ${email.isRead ? "Read" : "Unread"}`}
              >
                {getReadStatusIcon(email.isRead)}
                <span className="ml-1">{email.isRead ? "Read" : "Unread"}</span>
              </span>

              {/* Importance Badge */}
              <span
                className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                style={{
                  backgroundColor: `${email.importanceColor}20`,
                  color: email.importanceColor,
                  border: `1px solid ${email.importanceColor}40`,
                }}
                title={`Email importance: ${email.importance}`}
              >
                {getImportanceIcon(email.importance)}
                <span className="ml-1">{email.importance}</span>
              </span>

              {/* Match Reason for Search Results */}
              {email.matchReason && (
                <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-yellow-100 text-yellow-700" title={`Search match found in: ${email.matchReason}`}>
                  <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z" clipRule="evenodd" />
                  </svg>
                  Match: {email.matchReason}
                </span>
              )}
            </div>
          </div>

          {/* Open in Outlook button */}
          {email.webLink && (
            <div className="flex-shrink-0 ml-3">
              <a
                href={email.webLink}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-colors duration-200"
                style={{
                  background: "var(--accent-primary)",
                }}
                onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")}
                onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")}
                title="Open in Outlook"
              >
                <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M11 3a1 1 0 100 2h2.586l-6.293 6.293a1 1 0 101.414 1.414L15 6.414V9a1 1 0 102 0V4a1 1 0 00-1-1h-5z" />
                  <path d="M5 5a2 2 0 00-2 2v6a2 2 0 002 2h6a2 2 0 002-2v-3a1 1 0 10-2 0v3H5V7h3a1 1 0 000-2H5z" />
                </svg>
                Open
              </a>
            </div>
          )}
        </div>

        {/* Email Details */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
          {/* Received Date */}
          <div className="flex items-center">
            <svg className="w-4 h-4 mr-2" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
            </svg>
            <p className="font-medium text-sm" style={{ color: "var(--text-primary)" }} title={`Email received: ${email.receivedDate}`}>
              Received: {email.receivedDate}
            </p>
          </div>

          {/* From Email */}
          {email.fromEmail && (
            <div className="flex items-center">
              <svg className="w-4 h-4 mr-2" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
                <path d="M2.003 5.884L10 9.882l7.997-3.998A2 2 0 0016 4H4a2 2 0 00-1.997 1.884z" />
                <path d="M18 8.118l-8 4-8-4V14a2 2 0 002 2h12a2 2 0 002-2V8.118z" />
              </svg>
              <p className="font-medium text-sm truncate" style={{ color: "var(--text-primary)" }} title={`Email address: ${email.fromEmail}`}>
                {email.fromEmail}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
});

const EmailCard: React.FC<EmailCardProps> = ({ emails }) => {
  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Header */}
      <div
        className="mb-3 p-3 rounded-lg"
        style={{
          background: "var(--bg-secondary)",
          border: "1px solid var(--border-primary)",
        }}
        title={`Emails overview showing ${emails.length} ${emails.length === 1 ? "email" : "emails"}`}
      >
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center shadow-sm" style={{ background: "var(--accent-primary)" }} title="Emails icon">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path d="M2.003 5.884L10 9.882l7.997-3.998A2 2 0 0016 4H4a2 2 0 00-1.997 1.884z" />
              <path d="M18 8.118l-8 4-8-4V14a2 2 0 002 2h12a2 2 0 002-2V8.118z" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold" style={{ color: "var(--text-primary)", fontWeight: "700" }} title={`${emails.length} ${emails.length === 1 ? "email" : "emails"} found`}>
              {emails.length === 1 ? "Email" : `${emails.length} Emails`}
            </h3>
            <p className="text-sm" style={{ color: "var(--text-secondary)" }} title="Your recent emails from Outlook">
              Your recent emails
            </p>
          </div>
        </div>
      </div>

      {/* Emails */}
      <div className="space-y-2">
        {emails.length > 0 ? (
          emails.map((email, index) => <EmailItem key={email.id || index} email={email} index={index} />)
        ) : (
          /* No emails */
          <div
            className="rounded-xl p-6 text-center"
            style={{
              background: "var(--bg-secondary)",
              border: "1px solid var(--border-primary)",
            }}
          >
            <div className="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3" style={{ background: "var(--bg-tertiary)" }}>
              <svg className="w-6 h-6" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
                <path d="M2.003 5.884L10 9.882l7.997-3.998A2 2 0 0016 4H4a2 2 0 00-1.997 1.884z" />
                <path d="M18 8.118l-8 4-8-4V14a2 2 0 002 2h12a2 2 0 002-2V8.118z" />
              </svg>
            </div>
            <p style={{ color: "var(--text-secondary)" }}>No emails found. Check your Outlook inbox for new messages.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default React.memo(EmailCard); 