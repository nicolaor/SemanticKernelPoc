import React from "react";

interface SharePointSite {
  title: string;
  url: string;
  created: string;
  webTemplate: string;
  description?: string;
}

interface SharePointCardProps {
  sites: SharePointSite[];
}

// Helper functions
const getWebTemplateIcon = (webTemplate: string) => {
  switch (webTemplate.toLowerCase()) {
    case "group":
      return (
        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
          <path d="M13 6a3 3 0 11-6 0 3 3 0 016 0zM18 8a2 2 0 11-4 0 2 2 0 014 0zM14 15a4 4 0 00-8 0v3h8v-3zM6 8a2 2 0 11-4 0 2 2 0 014 0zM16 18v-3a5.972 5.972 0 00-.75-2.906A3.005 3.005 0 0119 15v3h-3zM4.75 12.094A5.973 5.973 0 004 15v3H1v-3a3 3 0 013.75-2.906z" />
        </svg>
      );
    case "teamchannel":
      return (
        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M18 5v8a2 2 0 01-2 2h-5l-5 4v-4H4a2 2 0 01-2-2V5a2 2 0 012-2h12a2 2 0 012 2zM7 8H5v2h2V8zm2 0h2v2H9V8zm6 0h-2v2h2V8z" clipRule="evenodd" />
        </svg>
      );
    case "sts":
    case "sitepagepublishing":
      return (
        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
        </svg>
      );
    default:
      return (
        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
        </svg>
      );
  }
};

const getWebTemplateLabel = (webTemplate: string) => {
  switch (webTemplate.toLowerCase()) {
    case "group":
      return "Team Site";
    case "teamchannel":
      return "Teams Channel";
    case "sts":
      return "Classic Site";
    case "sitepagepublishing":
      return "Publishing Site";
    default:
      return webTemplate;
  }
};

const getSiteColor = (index: number) => {
  const colors = [
    { from: "#8B5CF6", to: "#7C3AED" }, // Purple
    { from: "#3B82F6", to: "#1D4ED8" }, // Blue
    { from: "#10B981", to: "#047857" }, // Green
    { from: "#F59E0B", to: "#D97706" }, // Amber
    { from: "#EF4444", to: "#DC2626" }, // Red
    { from: "#06B6D4", to: "#0891B2" }, // Cyan
  ];
  return colors[index % colors.length];
};

// Individual site item component
const SiteItem: React.FC<{ site: SharePointSite; index: number }> = React.memo(({ site, index }) => {
  const siteColor = getSiteColor(index);

  return (
    <div
      key={site.url}
      className="group rounded-xl shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden"
      style={{
        background: "var(--card-bg)",
        border: "1px solid var(--border-primary)",
      }}
    >
      {/* Site Color Bar */}
      <div
        className="h-1"
        style={{
          background: `linear-gradient(to right, ${siteColor.from}, ${siteColor.to})`,
        }}
        title="Site color indicator - helps distinguish between different sites"
      ></div>

      <div className="p-4">
        {/* Site Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1 min-w-0">
            <h4 className="text-base font-semibold mb-1 transition-colors" style={{ color: "var(--text-primary)", fontWeight: "600" }} title={`SharePoint Site: ${site.title}`}>
              {site.title}
            </h4>

            {site.description && (
              <p className="text-sm mb-2 line-clamp-2" style={{ color: "var(--text-secondary)" }} title={`Site description: ${site.description}`}>
                {site.description}
              </p>
            )}

            <div className="flex items-center space-x-2 flex-wrap gap-y-1">
              {/* Site Type Badge */}
              <span
                className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium"
                style={{
                  backgroundColor: `${siteColor.from}20`,
                  color: siteColor.from,
                  border: `1px solid ${siteColor.from}40`,
                }}
                title={`Site type: ${getWebTemplateLabel(site.webTemplate)}`}
              >
                {getWebTemplateIcon(site.webTemplate)}
                <span className="ml-1">{getWebTemplateLabel(site.webTemplate)}</span>
              </span>
            </div>
          </div>

          {/* Open in SharePoint button */}
          <div className="flex-shrink-0 ml-3">
            <a
              href={site.url}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-colors duration-200"
              style={{
                background: "var(--accent-primary)",
              }}
              onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")}
              onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")}
              title="Open in SharePoint"
            >
              <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                <path d="M11 3a1 1 0 100 2h2.586l-6.293 6.293a1 1 0 101.414 1.414L15 6.414V9a1 1 0 102 0V4a1 1 0 00-1-1h-5z" />
                <path d="M5 5a2 2 0 00-2 2v6a2 2 0 002 2h6a2 2 0 002-2v-3a1 1 0 10-2 0v3H5V7h3a1 1 0 000-2H5z" />
              </svg>
              Open
            </a>
          </div>
        </div>

        {/* Site Details */}
        <div className="grid grid-cols-1 gap-3 text-sm">
          {/* Created Date and URL */}
          <div className="flex items-center justify-between">
            <div className="flex items-center">
              <svg className="w-4 h-4 mr-2" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
              </svg>
              <p className="font-medium text-sm" style={{ color: "var(--text-primary)" }} title={`Site created on: ${site.created}`}>
                Created: {site.created}
              </p>
            </div>
            
            <div className="flex items-center text-xs" style={{ color: "var(--text-tertiary)" }} title={`SharePoint URL: ${site.url}`}>
              <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M4.083 9h1.946c.089-1.546.383-2.97.837-4.118A6.004 6.004 0 004.083 9zM10 2a8 8 0 100 16 8 8 0 000-16zm0 2c-.076 0-.232.032-.465.262-.238.234-.497.623-.737 1.182-.389.907-.673 2.142-.766 3.556h3.936c-.093-1.414-.377-2.649-.766-3.556-.24-.559-.5-.948-.737-1.182C10.232 4.032 10.076 4 10 4zm3.971 5c-.089-1.546-.383-2.97-.837-4.118A6.004 6.004 0 0115.917 9h-1.946zm-2.003 2H8.032c.093 1.414.377 2.649.766 3.556.24.559.5.948.737 1.182.233.23.389.262.465.262.076 0 .232-.032.465-.262.238-.234.498-.623.737-1.182.389-.907.673-2.142.766-3.556zm1.166 4.118c.454-1.147.748-2.572.837-4.118h1.946a6.004 6.004 0 01-2.783 4.118zm-6.268 0C6.412 13.97 6.118 12.546 6.03 11H4.083a6.004 6.004 0 002.783 4.118z" clipRule="evenodd" />
              </svg>
              <span className="truncate max-w-xs">{new URL(site.url).hostname}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
});

const SharePointCard: React.FC<SharePointCardProps> = ({ sites }) => {
  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Header */}
      <div
        className="mb-3 p-3 rounded-lg"
        style={{
          background: "var(--bg-secondary)",
          border: "1px solid var(--border-primary)",
        }}
        title={`SharePoint sites overview showing ${sites.length} ${sites.length === 1 ? "site" : "sites"}`}
      >
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center shadow-sm" style={{ background: "var(--accent-primary)" }} title="SharePoint sites icon">
            <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <h3 className="text-lg font-bold" style={{ color: "var(--text-primary)", fontWeight: "700" }} title={`${sites.length} SharePoint ${sites.length === 1 ? "site" : "sites"} found`}>
              {sites.length === 1 ? "SharePoint Site" : `${sites.length} SharePoint Sites`}
            </h3>
            <p className="text-sm" style={{ color: "var(--text-secondary)" }} title="Your SharePoint sites and Teams channels">
              Your sites and channels
            </p>
          </div>
        </div>
      </div>

      {/* Sites */}
      <div className="space-y-2">
        {sites.length > 0 ? (
          sites.map((site, index) => <SiteItem key={site.url || index} site={site} index={index} />)
        ) : (
          /* No sites */
          <div
            className="rounded-xl p-6 text-center"
            style={{
              background: "var(--bg-secondary)",
              border: "1px solid var(--border-primary)",
            }}
          >
            <div className="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3" style={{ background: "var(--bg-tertiary)" }}>
              <svg className="w-6 h-6" style={{ color: "var(--text-tertiary)" }} fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
              </svg>
            </div>
            <p style={{ color: "var(--text-secondary)" }}>No SharePoint sites found. Try adjusting your search criteria or check your access permissions.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default React.memo(SharePointCard); 