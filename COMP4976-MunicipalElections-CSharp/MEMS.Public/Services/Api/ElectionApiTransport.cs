using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace MEMS.Public.Services.Api;

/// <summary>Only anonymous GET requests. Ballot selections and administrator cookies never go through this client.</summary>
public sealed class ElectionApiTransport(HttpClient http)
{
    public async Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> jsonType, CancellationToken ct,
        bool allowNotFound = false) where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.ParseAdd("application/json");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new ApiRequestException(response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "This municipality or its election information is no longer available. Choose a municipality again.",
                    HttpStatusCode.BadRequest => "The election filters are invalid. Clear the filters and try again.",
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "The election information could not be accessed. Please try again.",
                    HttpStatusCode.TooManyRequests => "The election service is busy. Please wait a moment and try again.",
                    _ => "The election service is temporarily unavailable. Please try again."
                });
            return await response.Content.ReadFromJsonAsync(jsonType, ct)
                ?? throw new ApiRequestException("The election service returned an incomplete response. Please try again.");
        }
        catch (HttpRequestException)
        { throw new ApiRequestException("We couldn't reach the election service. Check your connection and try again."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ApiRequestException("The election service took too long to respond. Please try again."); }
        catch (JsonException)
        { throw new ApiRequestException("The election service returned an invalid response. Please try again."); }
    }
}

// Explicit metadata keeps the shared response DTOs usable in trimmed WebAssembly publication.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<MunicipalityResponse>))]
[JsonSerializable(typeof(MunicipalityResponse))]
[JsonSerializable(typeof(List<PositionResponse>))]
[JsonSerializable(typeof(List<CandidateResponse>))]
[JsonSerializable(typeof(CandidateResponse))]
internal sealed partial class PublicApiJsonContext : JsonSerializerContext;
