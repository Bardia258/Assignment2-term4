using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Text.RegularExpressions;
using MEMS.Data;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace MEMS.Admin.Services;

public sealed partial class AdminImageStore(IWebHostEnvironment environment, IConfiguration configuration, ILogger<AdminImageStore> logger)
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public string RootPath { get; } = ResolveRoot(environment.ContentRootPath, configuration["Uploads:RootPath"] ?? "App_Data/uploads");
    [GeneratedRegex("^[a-f0-9]{32}\\.(png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    public static string ResolveRoot(string contentRoot, string configured)
    {
        var root = Path.GetFullPath(configured, contentRoot);
        var relative = Path.GetRelativePath(Path.GetFullPath(contentRoot), root);
        var ancestor = Path.GetRelativePath(root, Path.GetFullPath(contentRoot));
        var inside = relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative);
        if ((ancestor != ".." && !ancestor.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(ancestor))
            || (inside && !relative.StartsWith("App_Data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Upload storage must be a dedicated App_Data subdirectory or an external directory.");
        return root;
    }

    public string? Url(string? name, string category) => ValidCategory(category) && name is not null && SafeName().IsMatch(name)
        ? $"/uploads/{category}/{name}" : null;
    private static bool ValidCategory(string category) => category is "logos" or "candidates";

    public async Task<string> SaveAsync(Stream stream, string originalName, string contentType, long length,
        string category, CancellationToken ct = default)
    {
        if (!ValidCategory(category)) throw new ValidationException("Invalid image category.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var expected = extension switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => null };
        if (expected is null || !string.Equals(expected, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Choose a PNG, JPEG, or WebP image with a matching content type.");
        if (length <= 0 || length > MaxBytes) throw new ValidationException("Images must be nonempty and no larger than 2 MB.");
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new ValidationException("Images must be no larger than 2 MB.");
            await buffer.WriteAsync(bytes.AsMemory(0, read), ct);
        }
        using var data = SKData.CreateCopy(buffer.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null) throw new ValidationException("The uploaded file is not a supported image.");
        var format = expected switch { "image/png" => SKEncodedImageFormat.Png, "image/jpeg" => SKEncodedImageFormat.Jpeg, _ => SKEncodedImageFormat.Webp };
        if (codec.EncodedFormat != format) throw new ValidationException("Image content does not match its file type.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 16_000_000 || info.Width > 8000 || info.Height > 8000)
            throw new ValidationException("Images must be at most 16 megapixels and 8000 pixels per side.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ValidationException("The image could not be decoded completely.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        if (encoded is null) throw new ValidationException("The image could not be encoded.");
        var suffix = extension == ".jpeg" ? ".jpg" : extension;
        var name = Guid.NewGuid().ToString("N") + suffix;
        var directory = Path.Combine(RootPath, category);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await output.WriteAsync(encoded.ToArray(), ct); // Re-encoded pixels; no original metadata or trailing payload.
        }
        catch { if (created) File.Delete(path); throw; }
        return name;
    }

    public async Task DeleteIfUnreferencedAsync(ApplicationDbContext db, string? name, string category, CancellationToken ct = default)
    {
        if (Url(name, category) is null) return;
        try
        {
            // References may exist in another tenant. Never delete based only on a filtered query.
            var used = category == "logos"
                ? await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.LogoFileName == name, ct)
                : await db.Candidates.IgnoreQueryFilters().AnyAsync(c => c.PhotoFileName == name, ct);
            if (!used) File.Delete(Path.Combine(RootPath, category, name!));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DbUpdateException or DbException)
        { logger.LogWarning("Unreferenced image cleanup deferred for {Category}", category); }
    }
}
