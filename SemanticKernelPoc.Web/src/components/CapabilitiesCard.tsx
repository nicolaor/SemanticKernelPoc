import React from 'react';

interface CapabilitiesCardProps {
  data: {
    capabilities: string;
  };
}

const CapabilitiesCard: React.FC<CapabilitiesCardProps> = ({ data: _ }) => {
  return (
    <div className="w-full max-w-2xl mx-auto">
      <div 
        className="rounded-xl shadow-sm overflow-hidden"
        style={{
          background: 'var(--card-bg)',
          border: '1px solid var(--border-primary)'
        }}
      >
        {/* Header */}
        <div 
          className="px-6 py-4"
          style={{
            background: 'var(--bg-secondary)',
            borderBottom: '1px solid var(--border-primary)'
          }}
        >
          <div className="flex items-center space-x-3">
            <div 
              className="w-10 h-10 rounded-lg flex items-center justify-center"
              style={{ background: 'var(--accent-primary)' }}
            >
              <svg className="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 20 20">
                <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <div>
              <h3 
                className="text-lg font-bold"
                style={{ color: 'var(--text-primary)' }}
              >
                🤖 AI Assistant Capabilities
              </h3>
              <p 
                className="text-sm"
                style={{ color: 'var(--text-secondary)' }}
              >
                Here's what I can help you with
              </p>
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="p-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Calendar Management */}
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <div 
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ background: 'var(--accent-primary)20' }}
                >
                  <svg className="w-4 h-4" style={{ color: 'var(--accent-primary)' }} fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M6 2a1 1 0 00-1 1v1H4a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2h-1V3a1 1 0 10-2 0v1H7V3a1 1 0 00-1-1zm0 5a1 1 0 000 2h8a1 1 0 100-2H6z" clipRule="evenodd" />
                  </svg>
                </div>
                <h4 
                  className="font-semibold"
                  style={{ color: 'var(--text-primary)' }}
                >
                  📅 Calendar Management
                </h4>
              </div>
              <ul className="space-y-1 text-sm" style={{ color: 'var(--text-secondary)' }}>
                <li>• View upcoming events and today's schedule</li>
                <li>• Add new calendar events with attendees</li>
                <li>• Find available time slots for meetings</li>
                <li>• Get event counts for time periods</li>
              </ul>
            </div>

            {/* Note-Taking */}
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <div 
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ background: '#10b98120' }}
                >
                  <svg className="w-4 h-4 text-emerald-600" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M9 2a1 1 0 000 2h2a1 1 0 100-2H9z" />
                    <path fillRule="evenodd" d="M4 5a2 2 0 012-2v1a2 2 0 002 2h2a2 2 0 002-2V3a2 2 0 012 2v6h-3V8a1 1 0 10-2 0v3H4V5z" clipRule="evenodd" />
                  </svg>
                </div>
                <h4 
                  className="font-semibold"
                  style={{ color: 'var(--text-primary)' }}
                >
                  📝 Note-Taking
                </h4>
              </div>
              <ul className="space-y-1 text-sm" style={{ color: 'var(--text-secondary)' }}>
                <li>• Create notes as To Do tasks</li>
                <li>• Retrieve and search recent notes</li>
                <li>• Update or mark notes as complete</li>
                <li>• Organize with priorities and due dates</li>
              </ul>
            </div>

            {/* Email Management */}
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <div 
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ background: '#f9731620' }}
                >
                  <svg className="w-4 h-4 text-orange-600" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M2.003 5.884L10 9.882l7.997-3.998A2 2 0 0016 4H4a2 2 0 00-1.997 1.884z" />
                    <path d="M18 8.118l-8 4-8-4V14a2 2 0 002 2h12a2 2 0 002-2V8.118z" />
                  </svg>
                </div>
                <h4 
                  className="font-semibold"
                  style={{ color: 'var(--text-primary)' }}
                >
                  📧 Email Management
                </h4>
              </div>
              <ul className="space-y-1 text-sm" style={{ color: 'var(--text-secondary)' }}>
                <li>• Read recent emails with previews</li>
                <li>• Send emails with CC and importance levels</li>
                <li>• Search emails by subject or sender</li>
                <li>• Manage inbox and email metadata</li>
              </ul>
            </div>

            {/* SharePoint & OneDrive */}
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <div 
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ background: '#8b5cf620' }}
                >
                  <svg className="w-4 h-4 text-purple-600" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clipRule="evenodd" />
                  </svg>
                </div>
                <h4 
                  className="font-semibold"
                  style={{ color: 'var(--text-primary)' }}
                >
                  📁 SharePoint & OneDrive
                </h4>
              </div>
              <ul className="space-y-1 text-sm" style={{ color: 'var(--text-secondary)' }}>
                <li>• Browse and search SharePoint sites</li>
                <li>• View document libraries and files</li>
                <li>• Manage OneDrive files and folders</li>
                <li>• Access site details and metadata</li>
              </ul>
            </div>
          </div>

          {/* Footer */}
          <div 
            className="mt-6 pt-4 text-center"
            style={{ borderTop: '1px solid var(--border-primary)' }}
          >
            <p 
              className="text-sm"
              style={{ color: 'var(--text-secondary)' }}
            >
              💡 <strong>Ready to help!</strong> Just ask me about any of these tasks and I'll guide you through them step by step.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CapabilitiesCard; 