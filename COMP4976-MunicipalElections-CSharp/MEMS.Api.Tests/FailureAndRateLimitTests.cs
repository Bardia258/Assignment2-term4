using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Tests;

// Registered only by the test host; no production test endpoint is added.
[ApiController, Route("api/testing/failure")]
public sealed class FailureController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("Password=TEST-ONLY-DO-NOT-EXPOSE;Server=private-test-server");
}

public sealed class FailureAndRateLimitTests
{
    [Fact]
    public async Task UnexpectedFailuresNeverReturnExceptionOrCredentialDetails()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        var response = await client.GetAsync("/api/testing/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("DO-NOT-EXPOSE", body);
        Assert.DoesNotContain("private-test-server", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task RepeatedLoginAttemptsAreRateLimited()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        await MemsFactory.CsrfAsync(client);
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync("/api/auth/login",
                new MEMS.Shared.Contracts.AdminLoginRequest { Username = "missing-account", Password = factory.Password });
        }
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
    }
}
