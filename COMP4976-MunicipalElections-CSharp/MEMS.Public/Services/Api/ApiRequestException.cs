namespace MEMS.Public.Services.Api;

/// <summary>
/// Thrown by API clients when a request fails (network error, 5xx, unexpected status).
/// The message is safe to show to users; technical details stay in the logs only.
/// </summary>
public sealed class ApiRequestException(string userMessage, Exception? inner = null)
    : Exception(userMessage, inner);
