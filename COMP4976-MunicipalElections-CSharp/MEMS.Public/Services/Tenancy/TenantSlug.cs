using System.Text.RegularExpressions;

namespace MEMS.Public.Services.Tenancy;

/// <summary>Validation for municipality URL slugs. Same rule the API enforces (lowercase, hyphen-separated).</summary>
public static partial class TenantSlug
{
    public const int MaxLength = 100;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    /// <summary>Lower-cases and trims a route value, e.g. "Burnaby" -> "burnaby".</summary>
    public static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

    /// <summary>True if the (already normalized) slug is syntactically valid. Malformed slugs never reach the API.</summary>
    public static bool IsValid(string slug) => slug.Length is > 0 and <= MaxLength && Pattern().IsMatch(slug);
}
