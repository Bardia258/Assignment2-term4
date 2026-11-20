using System.Text.RegularExpressions;

namespace MunicipalElections.Web.Services;

public static class UploadOptions
{
    public static string ResolveRoot(string rootPath, string contentRoot)
    {
        var root = Path.GetFullPath(rootPath, contentRoot);
        var relative = Path.GetRelativePath(Path.GetFullPath(contentRoot), root);
        var insideContent = relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative);
        var parentRelative = Path.GetRelativePath(root, Path.GetFullPath(contentRoot));
        var ancestor = parentRelative != ".." && !parentRelative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(parentRelative);
        if (ancestor || (insideContent && !relative.StartsWith("App_Data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Upload storage must use a dedicated App_Data subdirectory or an external directory, outside the application source and wwwroot.");
        return root;
    }
}

/// <summary>Read-only URLs for photographs and logos already stored by the backend.</summary>
public interface IImageStorage
{
    string? Url(string? fileName, string category);
}

public sealed partial class ImageStorage : IImageStorage
{
    [GeneratedRegex("^[a-f0-9]{32}\\.(png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    public string? Url(string? fileName, string category) =>
        category is "logos" or "candidates" && fileName is not null && SafeName().IsMatch(fileName)
            ? $"/uploads/{category}/{fileName}" : null;
}
