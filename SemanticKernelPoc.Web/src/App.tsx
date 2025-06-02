import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { loginRequest } from "./config/authConfig";
import { useState, useRef, useEffect } from "react";
import type { ChatMessage } from "./types/chat";
import { useUserProfile } from "./hooks/useUserProfile";
import { apiService, ApiConnectionError } from "./services/apiService";
import MessageRenderer from "./components/MessageRenderer";

function App() {
  const { instance } = useMsal();
  const { userProfile, loading: profileLoading, error: profileError, isApiConnectionError } = useUserProfile();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [newMessage, setNewMessage] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [isClearingConversation, setIsClearingConversation] = useState(false);
  const [shouldFocus, setShouldFocus] = useState(false);
  const chatEndRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  // Auto-scroll to bottom on new message
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Auto-focus textarea when shouldFocus is true
  useEffect(() => {
    if (shouldFocus && textareaRef.current && !isLoading) {
      // Small delay to ensure DOM updates are complete
      const timer = setTimeout(() => {
        textareaRef.current?.focus();
        setShouldFocus(false);
      }, 100);

      return () => clearTimeout(timer);
    }
  }, [shouldFocus, isLoading]);

  // Additional focus restoration when loading completes
  useEffect(() => {
    if (!isLoading && textareaRef.current && userProfile) {
      const timer = setTimeout(() => {
        textareaRef.current?.focus();
      }, 50);

      return () => clearTimeout(timer);
    }
  }, [isLoading, userProfile]);

  // Handle MSAL redirect response
  useEffect(() => {
    const handleRedirectResponse = async () => {
      try {
        // Only handle redirect if MSAL is properly initialized
        if (instance && typeof instance.handleRedirectPromise === "function") {
          const response = await instance.handleRedirectPromise();
          if (response) {
            console.log("Authentication successful");
          }
        }
      } catch (error) {
        console.error("MSAL redirect handling error:", error);
      }
    };

    handleRedirectResponse();
  }, [instance]);

  const handleLogin = async () => {
    try {
      await instance.loginPopup(loginRequest);
      console.log("Login successful");
    } catch (error) {
      console.error("Login failed:", error);
      // Fallback to redirect login
      try {
        await instance.loginRedirect(loginRequest);
      } catch (redirectError) {
        console.error("Redirect login also failed:", redirectError);
      }
    }
  };

  const handleLogout = () => {
    instance.logoutPopup().catch((error) => {
      console.error("Logout failed:", error);
    });
  };

  const handleSendMessage = async () => {
    if (!newMessage.trim() || !userProfile || isLoading) return;

    console.log(`Sending message: "${newMessage}"`);

    const userMessage: ChatMessage = {
      id: Date.now().toString(),
      sessionId: `session_${userProfile.userId}_${new Date().toISOString().split('T')[0]}`,
      content: newMessage,
      userId: userProfile.userId,
      userName: userProfile.displayName,
      timestamp: new Date().toISOString(),
      isAiResponse: false,
    };

    // Add user message immediately
    setMessages((prev) => [...prev, userMessage]);
    setNewMessage("");
    setIsLoading(true);

    try {
      // Get access token
      const accounts = await instance.getAllAccounts();
      if (accounts.length === 0) {
        throw new Error("No authenticated accounts found");
      }

      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account: accounts[0],
      });

      if (!response.accessToken) {
        throw new Error("Failed to acquire access token");
      }

      // Send message to AI
      const aiResponse = await apiService.sendMessage(userMessage, response.accessToken);

      // Add AI response to messages
      setMessages((prev) => [...prev, aiResponse]);
    } catch (error) {
      console.error("Failed to send message:", error);

      // Determine error message based on error type
      let errorContent = "Sorry, I'm having trouble responding right now. Please try again later.";

      if (error instanceof ApiConnectionError) {
        errorContent = `🔌 **Connection Error**\n\n${error.message}\n\nPlease start the backend API server and try again.`;
      } else if (error instanceof Error) {
        errorContent = `❌ **Error**\n\n${error.message}`;
      }

      // Add error message
      const errorMessage: ChatMessage = {
        id: Date.now().toString() + "_error",
        sessionId: `session_${userProfile.userId}_${new Date().toISOString().split('T')[0]}`,
        content: errorContent,
        userId: "ai-assistant",
        userName: "AI Assistant",
        timestamp: new Date().toISOString(),
        isAiResponse: true,
      };

      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
      // Request focus restoration after everything is complete
      setShouldFocus(true);
    }
  };

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const handleClearConversation = async () => {
    if (!userProfile || isLoading || isClearingConversation || messages.length === 0) return;

    // Show confirmation dialog
    if (!window.confirm("Are you sure you want to clear this conversation? This action cannot be undone.")) {
      return;
    }

    setIsClearingConversation(true);

    try {
      // Get access token
      const accounts = await instance.getAllAccounts();
      if (accounts.length === 0) {
        throw new Error("No authenticated accounts found");
      }

      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account: accounts[0],
      });

      if (!response.accessToken) {
        throw new Error("Failed to acquire access token");
      }

      // Generate the current session ID (same logic as in handleSendMessage)
      const sessionId = `session_${userProfile.userId}_${new Date().toISOString().split('T')[0]}`;

      // Call the clear conversation API
      await apiService.clearConversation(sessionId, response.accessToken);

      // Clear messages from UI
      setMessages([]);
      
      // Focus the input after clearing
      setShouldFocus(true);

    } catch (error) {
      console.error("Failed to clear conversation:", error);
      
      // Show error message
      let errorContent = "Failed to clear conversation. Please try again.";

      if (error instanceof ApiConnectionError) {
        errorContent = `🔌 **Connection Error**\n\n${error.message}\n\nPlease ensure the API server is running.`;
      } else if (error instanceof Error) {
        errorContent = `❌ **Error**\n\n${error.message}`;
      }

      // Add error message to chat
      const errorMessage: ChatMessage = {
        id: Date.now().toString() + "_clear_error",
        sessionId: `session_${userProfile.userId}_${new Date().toISOString().split('T')[0]}`,
        content: errorContent,
        userId: "ai-assistant",
        userName: "AI Assistant",
        timestamp: new Date().toISOString(),
        isAiResponse: true,
      };

      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsClearingConversation(false);
    }
  };

  return (
    <div
      className="h-screen flex flex-col overflow-hidden"
      style={{
        background: "var(--bg-primary)",
        color: "var(--text-primary)",
      }}
    >
      <nav
        className="shadow-lg"
        style={{
          background: "var(--nav-bg)",
          borderBottom: "1px solid var(--border-primary)",
        }}
      >
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16 items-center">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: "var(--accent-primary)" }}>
                <svg className="w-5 h-5 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <h1 className="text-2xl font-bold tracking-tight" style={{ color: "var(--text-primary)" }}>
                Semantic Kernel Chat
              </h1>
            </div>
            <div className="flex items-center space-x-4">
              <AuthenticatedTemplate>
                {profileLoading ? (
                  <div style={{ color: "var(--text-secondary)" }} className="text-sm">
                    Loading...
                  </div>
                ) : userProfile ? (
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full flex items-center justify-center" style={{ background: "var(--accent-primary)" }}>
                      <span className="text-white text-xs font-bold">{userProfile.initials}</span>
                    </div>
                    <span className="text-sm font-medium" style={{ color: "var(--text-primary)" }}>
                      {userProfile.displayName}
                    </span>
                  </div>
                ) : null}
                <button
                  onClick={handleLogout}
                  className="px-5 py-2 text-sm font-semibold rounded-lg text-white transition-all duration-200"
                  style={{
                    background: "var(--accent-primary)",
                    border: "1px solid var(--accent-secondary)",
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")}
                >
                  Sign Out
                </button>
              </AuthenticatedTemplate>
              <UnauthenticatedTemplate>
                <button onClick={handleLogin} className="px-6 py-2 text-sm font-semibold rounded-lg text-white shadow-lg hover:shadow-xl transition-all duration-200" style={{ background: "var(--accent-primary)" }} onMouseEnter={(e) => (e.currentTarget.style.background = "var(--accent-secondary)")} onMouseLeave={(e) => (e.currentTarget.style.background = "var(--accent-primary)")}>
                  Sign In
                </button>
              </UnauthenticatedTemplate>
            </div>
          </div>
        </div>
      </nav>

      <main className="flex-1 flex flex-col items-center py-4 px-4 overflow-hidden">
        <UnauthenticatedTemplate>
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center max-w-md">
              <div className="w-20 h-20 rounded-full flex items-center justify-center mx-auto mb-6" style={{ background: "var(--accent-primary)" }}>
                <svg className="w-10 h-10 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M18 10c0 3.866-3.582 7-8 7a8.841 8.841 0 01-4.083-.98L2 17l1.338-3.123C2.493 12.767 2 11.434 2 10c0-3.866 3.582-7 8-7s8 3.134 8 7z" />
                </svg>
              </div>
              <h2 className="text-3xl font-bold mb-4" style={{ color: "var(--text-primary)" }}>
                Welcome to Semantic Kernel Chat
              </h2>
              <p className="text-lg mb-8" style={{ color: "var(--text-secondary)" }}>
                Sign in with your Azure AD account to start chatting with your AI assistant.
              </p>
            </div>
          </div>
        </UnauthenticatedTemplate>
        <AuthenticatedTemplate>
          {/* API Connection Error Banner */}
          {isApiConnectionError && profileError && (
            <div
              className="w-full max-w-5xl mb-4 p-4 rounded-lg border-l-4"
              style={{
                background: "var(--bg-secondary)",
                borderLeftColor: "#ef4444",
                border: "1px solid var(--border-primary)",
              }}
            >
              <div className="flex items-start space-x-3">
                <div className="flex-shrink-0">
                  <svg className="w-5 h-5 text-red-500" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                  </svg>
                </div>
                <div className="flex-1">
                  <h3 className="text-sm font-semibold text-red-800" style={{ color: "#dc2626" }}>
                    Backend API Connection Failed
                  </h3>
                  <p className="text-sm mt-1" style={{ color: "var(--text-secondary)" }}>
                    {profileError}
                  </p>
                  <div className="mt-2">
                    <p className="text-xs" style={{ color: "var(--text-tertiary)" }}>
                      💡 <strong>To fix this:</strong> Start the backend API server by running the .NET project on http://localhost:5040
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}
          <div
            className="w-full max-w-5xl flex flex-col backdrop-blur-sm shadow-2xl rounded-3xl overflow-hidden"
            style={{
              background: "var(--card-bg)",
              border: "1px solid var(--border-primary)",
              height: "calc(100vh - 6rem)", // Optimized: viewport minus nav (4rem) and padding (2rem)
            }}
          >
            <div
              className="px-4 py-2 flex-shrink-0"
              style={{
                background: "var(--bg-secondary)",
                borderBottom: "1px solid var(--border-primary)",
              }}
            >
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-base font-semibold" style={{ color: "var(--text-primary)" }}>
                    AI Assistant
                  </h2>
                  <p className="text-xs" style={{ color: "var(--text-secondary)" }}>
                    Powered by Semantic Kernel
                  </p>
                </div>
                {messages.length > 0 && (
                  <button
                    onClick={handleClearConversation}
                    className="flex items-center space-x-1 px-3 py-1.5 rounded-lg text-xs font-medium transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                    style={{
                      color: "var(--text-secondary)",
                      border: "1px solid var(--border-primary)",
                      background: "var(--card-bg)",
                    }}
                    onMouseEnter={(e) => !e.currentTarget.disabled && (e.currentTarget.style.background = "var(--bg-tertiary)")}
                    onMouseLeave={(e) => !e.currentTarget.disabled && (e.currentTarget.style.background = "var(--card-bg)")}
                    disabled={!userProfile || isLoading || isClearingConversation}
                    title="Clear conversation"
                  >
                    {isClearingConversation ? (
                      <>
                        <div className="w-3 h-3 border border-current border-t-transparent rounded-full animate-spin"></div>
                        <span>Clearing...</span>
                      </>
                    ) : (
                      <>
                        <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1-1H9a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
                        <span>Clear</span>
                      </>
                    )}
                  </button>
                )}
              </div>
            </div>
            <div className={`flex-1 px-4 py-1 ${messages.length === 0 ? "overflow-hidden" : "overflow-y-auto space-y-3"}`}>
              {messages.length === 0 ? (
                <div className="flex items-center justify-center h-full">
                  <div className="text-center">
                    <div className="w-6 h-6 rounded-full flex items-center justify-center mx-auto mb-1" style={{ background: "var(--accent-primary)" }}>
                      <svg className="w-3 h-3 text-white" fill="currentColor" viewBox="0 0 20 20">
                        <path d="M18 10c0 3.866-3.582 7-8 7a8.841 8.841 0 01-4.083-.98L2 17l1.338-3.123C2.493 12.767 2 11.434 2 10c0-3.866 3.582-7 8-7s8 3.134 8 7zM7 9H5v2h2V9zm8 0h-2v2h2V9zM9 9h2v2H9V9z" />
                      </svg>
                    </div>
                    <p className="text-sm font-medium" style={{ color: "var(--text-primary)" }}>
                      Welcome
                      {userProfile ? `, ${userProfile.givenName || userProfile.displayName}` : ""}!
                    </p>
                    <p className="text-xs" style={{ color: "var(--text-secondary)" }}>
                      Start chatting with your AI assistant
                    </p>
                  </div>
                </div>
              ) : (
                messages.map((message) => (
                  <div key={message.id} className={`flex ${message.isAiResponse ? "justify-start" : "justify-end"}`}>
                    <div className={`flex flex-col max-w-[75%] ${message.isAiResponse ? "items-start" : "items-end"}`}>
                      <div className={`flex items-center space-x-2 mb-1 ${message.isAiResponse ? "" : "flex-row-reverse space-x-reverse"}`}>
                        <span className="text-xs font-medium" style={{ color: "var(--text-secondary)" }}>
                          {message.userName}
                        </span>
                        <span className="text-xs" style={{ color: "var(--text-tertiary)" }}>
                          {new Date(message.timestamp).toLocaleTimeString()}
                        </span>
                      </div>
                      <div className={`flex items-start space-x-2 ${message.isAiResponse ? "" : "flex-row-reverse space-x-reverse"}`}>
                        <div
                          className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0"
                          style={{
                            background: message.isAiResponse ? "var(--accent-primary)" : "var(--accent-secondary)",
                          }}
                        >
                          <span className="text-white text-xs font-bold">{message.isAiResponse ? "AI" : userProfile?.initials || "U"}</span>
                        </div>
                        <div
                          className="px-3 py-2 rounded-2xl shadow-md text-sm"
                          style={{
                            background: message.isAiResponse ? "var(--card-bg)" : "var(--accent-primary)",
                            color: message.isAiResponse ? "var(--text-primary)" : "#ffffff",
                            border: message.isAiResponse ? "1px solid var(--border-primary)" : "none",
                          }}
                        >
                          <MessageRenderer message={message} />
                        </div>
                      </div>
                    </div>
                  </div>
                ))
              )}
              {isLoading && (
                <div className="flex justify-start">
                  <div className="flex flex-col max-w-[75%] items-start">
                    <div className="flex items-center space-x-2 mb-1">
                      <span className="text-xs font-medium" style={{ color: "var(--text-secondary)" }}>
                        AI Assistant
                      </span>
                      <span className="text-xs" style={{ color: "var(--text-tertiary)" }}>
                        {new Date().toLocaleTimeString()}
                      </span>
                    </div>
                    <div className="flex items-start space-x-2">
                      <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "var(--accent-primary)" }}>
                        <span className="text-white text-xs font-bold">AI</span>
                      </div>
                      <div
                        className="px-3 py-2 rounded-2xl shadow-md text-sm"
                        style={{
                          background: "var(--card-bg)",
                          color: "var(--text-primary)",
                          border: "1px solid var(--border-primary)",
                        }}
                      >
                        <div className="flex items-center space-x-2">
                          <div className="flex space-x-1">
                            <div className="w-2 h-2 rounded-full animate-bounce" style={{ background: "var(--accent-primary)" }}></div>
                            <div
                              className="w-2 h-2 rounded-full animate-bounce"
                              style={{
                                background: "var(--accent-primary)",
                                animationDelay: "0.1s",
                              }}
                            ></div>
                            <div
                              className="w-2 h-2 rounded-full animate-bounce"
                              style={{
                                background: "var(--accent-primary)",
                                animationDelay: "0.2s",
                              }}
                            ></div>
                          </div>
                          <span style={{ color: "var(--text-secondary)" }}>AI is thinking...</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}
              <div ref={chatEndRef} />
            </div>
            <div
              className="px-4 py-2 flex-shrink-0"
              style={{
                background: "var(--bg-secondary)",
                borderTop: "1px solid var(--border-primary)",
              }}
            >
              <div className="flex items-end gap-2">
                <textarea
                  value={newMessage}
                  onChange={(e) => setNewMessage(e.target.value)}
                  onKeyDown={handleInputKeyDown}
                  placeholder={isLoading ? "AI is responding..." : "Type your message..."}
                  rows={1}
                  className="flex-1 resize-none px-3 py-2 rounded-2xl backdrop-blur-sm focus:outline-none focus:ring-2 shadow-sm text-sm"
                  style={{
                    minHeight: 40,
                    maxHeight: 100,
                    background: "var(--input-bg)",
                    color: "var(--text-primary)",
                    border: "1px solid var(--border-primary)",
                  }}
                  disabled={!userProfile || isLoading}
                  ref={textareaRef}
                />
                <button
                  onClick={handleSendMessage}
                  className="flex items-center justify-center p-2 rounded-2xl text-white font-semibold shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                  style={{
                    minHeight: 40,
                    minWidth: 40,
                    background: "var(--accent-primary)",
                  }}
                  onMouseEnter={(e) => !e.currentTarget.disabled && (e.currentTarget.style.background = "var(--accent-secondary)")}
                  onMouseLeave={(e) => !e.currentTarget.disabled && (e.currentTarget.style.background = "var(--accent-primary)")}
                  disabled={!newMessage.trim() || !userProfile || isLoading}
                  aria-label="Send"
                >
                  {isLoading ? (
                    <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                  ) : (
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={2} stroke="currentColor" className="w-5 h-5">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M6 12L3.269 3.126A59.768 59.768 0 0121.485 12 59.77 59.77 0 013.27 20.876L5.999 12zm0 0h7.5" />
                    </svg>
                  )}
                </button>
              </div>
            </div>
          </div>
        </AuthenticatedTemplate>
      </main>
    </div>
  );
}

export default App;
