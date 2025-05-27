using System.Diagnostics.CodeAnalysis;

// Suppress VSTHRD200 warnings for KernelFunction methods
// These methods are used by Semantic Kernel and don't need "Async" suffix
[assembly: SuppressMessage("Usage", "VSTHRD200:Use \"Async\" suffix in names of methods that return an awaitable type", Justification = "KernelFunction methods don't require Async suffix")] 