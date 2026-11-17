using System.Net.Mail;

namespace MEMS.Public.Services;

/// <summary>
/// Guards links built from API data (candidate websites and emails) so values such as
/// "javascript:..." can never end up in an href.
/// </summary>
public static class SafeLinks
{
    /// <summary>Returns an absolute http(s) URL, or <c>null</c> if the value isn't one.</summary>
    public static Uri? HttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    /// <summary>Short display text for a website link, e.g. "example.org/about".</summary>
    public static string DisplayText(Uri uri)
    {
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var path = uri.AbsolutePath.TrimEnd('/');
        return host + path;
    }

    /// <summary>Returns a "mailto:" href for a well-formed address, or <c>null</c>.</summary>
    public static string? MailTo(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out var address)
            || address.Address != trimmed)
            return null;
        return $"mailto:{Uri.EscapeDataString(address.User)}@{address.Host}";
    }
}
