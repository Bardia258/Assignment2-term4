using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Contracts;

public sealed class AdminLoginRequest
{
    [Required, StringLength(254)] public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}

public sealed record AdminSessionResponse(string UserId, string Username, string Role, int? MunicipalityId);
public sealed record CsrfResponse(string RequestToken, string HeaderName);
