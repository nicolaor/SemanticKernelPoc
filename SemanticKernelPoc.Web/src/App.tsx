import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { loginRequest } from "./config/authConfig";
import { useState, useRef, useEffect } from "react";
import type { ChatMessage } from "./types/chat";
import { useUserProfile } from "./hooks/useUserProfile";
import { apiService } from "./services/apiService";
import MessageRenderer from "./components/MessageRenderer";

function App() {
  const { instance } = useMsal();
  const { userProfile, loading: profileLoading } = useUserProfile();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [newMessage, setNewMessage] = useState("");
  const [isLoading, setIsLoading] = useState(false);
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

  // Handle redirect responses and initialize MSAL
  useEffect(() => {
    const initializeMsal = async () => {
      try {
        console.log("Initializing MSAL...");
        
        // Initialize MSAL first
        await instance.initialize();
        console.log("MSAL initialized successfully");
        
        // Then handle any redirect responses
        const response = await instance.handleRedirectPromise();
        if (response) {
          console.log("Redirect response received:", response);
        }
      } catch (error) {
        console.error("MSAL initialization error:", error);
      }
    };

    initializeMsal();
  }, [instance]);

  const handleLogin = () => {
    console.log("Attempting login...");
    instance.loginPopup(loginRequest)
      .then((response) => {
        console.log("Login successful:", response);
      })
      .catch(error => {
        console.error("Popup login failed:", error);
        
        // Fallback to redirect if popup fails
        if (error.errorMessage && error.errorMessage.includes("popup")) {
          console.log("Falling back to redirect login...");
          instance.loginRedirect(loginRequest);
        }
      });
  };

  const handleRedirectLogin = () => {
    console.log("Attempting redirect login...");
    instance.loginRedirect(loginRequest);
  };

  const handleLogout = () => {
    instance.logoutPopup().catch(error => {
      console.error("Logout failed:", error);
    });
  };

  const handleSendMessage = async () => {
    if (!newMessage.trim() || !userProfile || isLoading) return;
    
    const userMessage: ChatMessage = {
      id: Date.now().toString(),
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
        account: accounts[0]
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
      
      // Add error message
      const errorMessage: ChatMessage = {
        id: Date.now().toString() + "_error",
        content: "Sorry, I'm having trouble responding right now. Please try again later.",
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

  return (
    <div
      className="min-h-screen bg-gradient-to-br from-blue-50 via-indigo-50 to-purple-50 flex flex-col"
      style={{ color: "#1e293b" }}
    >
      <nav className="bg-gradient-to-r from-blue-600 to-indigo-700 shadow-lg border-b border-blue-700/20">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16 items-center">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-white/20 rounded-lg flex items-center justify-center">
                <svg className="w-5 h-5 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <h1 className="text-2xl font-bold text-white tracking-tight">Semantic Kernel Chat</h1>
            </div>
            <div className="flex items-center space-x-4">
              <AuthenticatedTemplate>
                {profileLoading ? (
                  <div className="text-white/80 text-sm">Loading...</div>
                ) : userProfile ? (
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 bg-white/20 rounded-full flex items-center justify-center">
                      <span className="text-white text-xs font-bold">
                        {userProfile.initials}
                      </span>
                    </div>
                    <span className="text-white text-sm font-medium">
                      {userProfile.displayName}
                    </span>
                  </div>
                ) : null}
                <button
                  onClick={handleLogout}
                  className="px-5 py-2 text-sm font-semibold rounded-lg bg-white/10 hover:bg-white/20 text-white border border-white/20 hover:border-white/30 transition-all duration-200 backdrop-blur-sm"
                >
                  Sign Out
                </button>
              </AuthenticatedTemplate>
              <UnauthenticatedTemplate>
                <div className="text-center py-16">
                  <div className="w-20 h-20 bg-gradient-to-r from-blue-600 to-indigo-700 rounded-full flex items-center justify-center mx-auto mb-6">
                    <svg className="w-10 h-10 text-white" fill="currentColor" viewBox="0 0 20 20">
                      <path d="M18 10c0 3.866-3.582 7-8 7a8.841 8.841 0 01-4.083-.98L2 17l1.338-3.123C2.493 12.767 2 11.434 2 10c0-3.866 3.582-7 8-7s8 3.134 8 7z" />
                    </svg>
                  </div>
                  <h2 className="text-3xl font-bold text-gray-900 mb-4">
                    Welcome to Semantic Kernel Chat
                  </h2>
                  <p className="text-lg text-gray-600 mb-8 max-w-md mx-auto">
                    Sign in with your Azure AD account to start chatting with your AI assistant.
                  </p>
                  <div className="space-y-4">
                    <button
                      onClick={handleLogin}
                      className="px-8 py-3 text-lg font-semibold rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white shadow-xl hover:shadow-2xl transition-all duration-200"
                    >
                      Sign In (Popup)
                    </button>
                    <div>
                      <button
                        onClick={handleRedirectLogin}
                        className="px-6 py-2 text-sm font-semibold rounded-lg bg-gray-600 hover:bg-gray-700 text-white shadow-lg hover:shadow-xl transition-all duration-200"
                      >
                        Try Redirect Login
                      </button>
                      <p className="text-sm text-gray-500 mt-2">
                        Use this if popup is blocked or not working
                      </p>
                    </div>
                  </div>
                </div>
              </UnauthenticatedTemplate>
            </div>
          </div>
        </div>
      </nav>

      <main className="flex-1 flex flex-col items-center py-8 px-4 min-h-0">
        <AuthenticatedTemplate>
          <div className="w-full max-w-4xl flex-1 flex flex-col bg-white/80 backdrop-blur-sm shadow-2xl rounded-3xl border border-blue-200/50 overflow-hidden min-h-0">
            <div className="bg-gradient-to-r from-blue-500/5 to-indigo-500/5 px-6 py-4 border-b border-blue-200/30 flex-shrink-0">
              <h2 className="text-lg font-semibold text-gray-800">AI Assistant</h2>
              <p className="text-sm text-gray-600">Powered by Semantic Kernel</p>
            </div>
            <div className="flex-1 overflow-y-auto px-6 py-6 space-y-6 min-h-0">
              {messages.length === 0 ? (
                <div className="flex items-center justify-center h-full">
                  <div className="text-center">
                    <div className="w-16 h-16 bg-gradient-to-r from-blue-500 to-indigo-600 rounded-full flex items-center justify-center mx-auto mb-4">
                      <svg className="w-8 h-8 text-white" fill="currentColor" viewBox="0 0 20 20">
                        <path d="M18 10c0 3.866-3.582 7-8 7a8.841 8.841 0 01-4.083-.98L2 17l1.338-3.123C2.493 12.767 2 11.434 2 10c0-3.866 3.582-7 8-7s8 3.134 8 7zM7 9H5v2h2V9zm8 0h-2v2h2V9zM9 9h2v2H9V9z" />
                      </svg>
                    </div>
                    <p className="text-gray-500 text-lg">Welcome{userProfile ? `, ${userProfile.givenName || userProfile.displayName}` : ''}! Start a conversation with your AI assistant.</p>
                    <p className="text-gray-400 text-sm mt-2">Type your message below to get started.</p>
                  </div>
                </div>
              ) : (
                messages.map((message) => (
                  <div
                    key={message.id}
                    className={`flex ${message.isAiResponse ? "justify-start" : "justify-end"}`}
                  >
                    <div className={`flex flex-col max-w-[75%] ${message.isAiResponse ? "items-start" : "items-end"}`}>
                      <div className={`flex items-center space-x-2 mb-1 ${message.isAiResponse ? "" : "flex-row-reverse space-x-reverse"}`}>
                        <span className="text-xs text-gray-500 font-medium">{message.userName}</span>
                        <span className="text-xs text-gray-400">
                          {new Date(message.timestamp).toLocaleTimeString()}
                        </span>
                      </div>
                      <div className={`flex items-start space-x-3 ${message.isAiResponse ? "" : "flex-row-reverse space-x-reverse"}`}>
                        <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 ${
                          message.isAiResponse 
                            ? "bg-gradient-to-r from-blue-500 to-indigo-600" 
                            : "bg-gradient-to-r from-gray-600 to-gray-700"
                        }`}>
                          <span className="text-white text-xs font-bold">
                            {message.isAiResponse ? "AI" : (userProfile?.initials || "U")}
                          </span>
                        </div>
                        <div
                          className={`px-4 py-3 rounded-2xl shadow-md text-base ${
                            message.isAiResponse
                              ? "bg-gradient-to-r from-blue-50 to-indigo-50 text-gray-800 border border-blue-200/50"
                              : "bg-gradient-to-r from-blue-600 to-indigo-600 text-white"
                          }`}
                        >
                          <MessageRenderer content={message.content} isAiResponse={message.isAiResponse} />
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
                      <span className="text-xs text-gray-500 font-medium">AI Assistant</span>
                      <span className="text-xs text-gray-400">
                        {new Date().toLocaleTimeString()}
                      </span>
                    </div>
                    <div className="flex items-start space-x-3">
                      <div className="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 bg-gradient-to-r from-blue-500 to-indigo-600">
                        <span className="text-white text-xs font-bold">AI</span>
                      </div>
                      <div className="px-4 py-3 rounded-2xl shadow-md text-base bg-gradient-to-r from-blue-50 to-indigo-50 text-gray-800 border border-blue-200/50">
                        <div className="flex items-center space-x-2">
                          <div className="flex space-x-1">
                            <div className="w-2 h-2 bg-blue-500 rounded-full animate-bounce"></div>
                            <div className="w-2 h-2 bg-blue-500 rounded-full animate-bounce" style={{ animationDelay: "0.1s" }}></div>
                            <div className="w-2 h-2 bg-blue-500 rounded-full animate-bounce" style={{ animationDelay: "0.2s" }}></div>
                          </div>
                          <span className="text-gray-600">AI is thinking...</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}
              <div ref={chatEndRef} />
            </div>
            <div className="bg-gradient-to-r from-blue-500/5 to-indigo-500/5 border-t border-blue-200/30 px-6 py-4 flex-shrink-0">
              <div className="flex items-end gap-3">
                <textarea
                  value={newMessage}
                  onChange={(e) => setNewMessage(e.target.value)}
                  onKeyDown={handleInputKeyDown}
                  placeholder={isLoading ? "AI is responding..." : "Type your message..."}
                  rows={1}
                  className="flex-1 resize-none px-4 py-3 border border-blue-200 rounded-2xl bg-white/70 backdrop-blur-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 shadow-sm placeholder-gray-500"
                  style={{ minHeight: 48, maxHeight: 120 }}
                  disabled={!userProfile || isLoading}
                  ref={textareaRef}
                />
                <button
                  onClick={handleSendMessage}
                  className="flex items-center justify-center p-3 rounded-2xl bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white font-semibold shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                  style={{ minHeight: 48, minWidth: 48 }}
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
