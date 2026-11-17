using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MEMS.Api.Security;

/// <summary>Validate cookie-authenticated unsafe requests, including login CSRF, without MVC view services.</summary>
public sealed class ApiAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)) return;
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = "Invalid antiforgery token",
                Detail = "Get a current token from /api/auth/csrf and send it in X-CSRF-TOKEN with its companion cookie.",
                Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
            }) { ContentTypes = { "application/problem+json" } };
        }
    }
}
