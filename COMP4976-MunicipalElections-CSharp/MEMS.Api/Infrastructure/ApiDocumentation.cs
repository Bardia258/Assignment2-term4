using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MEMS.Api.Infrastructure;

public static class ApiDocumentation
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "MEMS election API", Version = "v1",
                Description = "Public election reads require a municipality ID or slug, except the municipality directory. " +
                    "Administrator cookies never expand a municipality administrator's assignment. " +
                    "Unsafe requests require X-CSRF-TOKEN and its companion cookie; see MEMS.Api/README.md for examples."
            };
            document.Components ??= new();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["AdminCookie"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie,
                    Name = "__Host-MEMS.Admin", Description = "Secure HttpOnly Identity cookie issued by POST /api/auth/login." },
                ["CsrfHeader"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header,
                    Name = "X-CSRF-TOKEN", Description = "Request token from GET /api/auth/csrf. Refresh after signing in." },
                ["CsrfCookie"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie,
                    Name = "__Host-MEMS.Csrf", Description = "Secure HttpOnly companion cookie set by the CSRF endpoint." }
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(async (operation, context, ct) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var authenticated = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
            var unsafeMethod = context.Description.HttpMethod is "POST" or "PUT" or "DELETE" or "PATCH";
            if (authenticated || unsafeMethod)
            {
                var requirement = new OpenApiSecurityRequirement();
                if (authenticated) requirement[new OpenApiSecuritySchemeReference("AdminCookie", context.Document!)] = [];
                if (unsafeMethod)
                {
                    requirement[new OpenApiSecuritySchemeReference("CsrfHeader", context.Document!)] = [];
                    requirement[new OpenApiSecuritySchemeReference("CsrfCookie", context.Document!)] = [];
                }
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
            operation.Description = (operation.Description ?? "") + (authenticated
                ? " Requires an active administrator cookie and current database assignment."
                : " Public endpoint.");
            if (unsafeMethod) operation.Description += " Requires CSRF header and companion cookie.";
            if (context.Description.RelativePath?.Contains("municipalities") == true && unsafeMethod)
                operation.Description += " Only Super Admin may manage municipalities.";
            operation.Responses ??= new OpenApiResponses();
            var problem = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct);
            var validation = await context.GetOrCreateSchemaAsync(typeof(ValidationProblemDetails), null, ct);
            foreach (var (status, description) in new Dictionary<string, string>
            {
                ["400"] = "Invalid input, conflicting filters, missing public tenant, or invalid CSRF token.",
                ["401"] = "Authentication required or invalid credentials.",
                ["403"] = "Operation outside administrator permissions.",
                ["404"] = "Record not found within the effective municipality scope.",
                ["409"] = "Duplicate record, dependent records, or stale revision.",
                ["429"] = "Login rate limit exceeded.",
                ["500"] = "Unexpected server error; details are not exposed.",
                ["503"] = "Database temporarily unavailable."
            })
                operation.Responses.TryAdd(status, new OpenApiResponse
                {
                    Description = description,
                    Content = new Dictionary<string, OpenApiMediaType>
                    { ["application/problem+json"] = new() { Schema = status == "400" ? validation : problem } }
                });
        });
    }
}
