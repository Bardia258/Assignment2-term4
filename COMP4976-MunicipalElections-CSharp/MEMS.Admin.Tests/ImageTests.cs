using System.ComponentModel.DataAnnotations;
using MEMS.Admin.Services;
using MEMS.Data;
using MEMS.Data.Tenancy;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace MEMS.Admin.Tests;

public sealed class TestImage(byte[] bytes, string name = "photo.png", string type = "image/png", long? declaredLength = null) : IBrowserFile
{
    public string Name => name;
    public string ContentType => type;
    public long Size => declaredLength ?? bytes.Length;
    public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(bytes);
}

public sealed class ImageTests
{
    public static byte[] Png(int width = 2, int height = 2)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    [Fact]
    public async Task UploadReplacementPreservesOldFileOnFailureAndRemovesUnreferencedImages()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Burnabyadmin", async (data, services) =>
        {
            var images = services.GetRequiredService<AdminImageStore>();
            var item = (await data.CandidatesAsync())[0];
            var input = new CandidateRequest { Name = item.Name, Profile = item.Profile,
                MunicipalityId = item.MunicipalityId, PositionId = item.PositionId };
            await data.SaveCandidateAsync(item.Id, input, item.Revision, new TestImage(Png(), "../untrusted-name.png"));
            var uploaded = await data.CandidateAsync(item.Id);
            var oldName = Path.GetFileName(uploaded.PhotoUrl!);
            Assert.Matches("^[a-f0-9]{32}\\.png$", oldName);
            Assert.True(File.Exists(Path.Combine(images.RootPath, "candidates", oldName)));
            await Assert.ThrowsAsync<ValidationException>(() => data.SaveCandidateAsync(item.Id, input, uploaded.Revision,
                new TestImage(System.Text.Encoding.UTF8.GetBytes("<svg>not a PNG</svg>"))));
            Assert.True(File.Exists(Path.Combine(images.RootPath, "candidates", oldName)));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => data.SaveCandidateAsync(item.Id, input, item.Revision, new TestImage(Png())));
            Assert.Single(Directory.GetFiles(Path.Combine(images.RootPath, "candidates")));
            await data.SaveCandidateAsync(item.Id, input, uploaded.Revision, new TestImage(Png()));
            var replaced = await data.CandidateAsync(item.Id);
            Assert.NotEqual(uploaded.PhotoUrl, replaced.PhotoUrl);
            Assert.False(File.Exists(Path.Combine(images.RootPath, "candidates", oldName)));
            var newPath = Path.Combine(images.RootPath, "candidates", Path.GetFileName(replaced.PhotoUrl!));
            await data.DeleteCandidateAsync(item.Id, replaced.Revision);
            Assert.False(File.Exists(newPath));
            return true;
        });
    }

    [Fact]
    public async Task CleanupChecksReferencesAcrossAllTenants()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Burnabyadmin", async (data, services) =>
        {
            var item = (await data.CandidatesAsync())[0];
            await data.SaveCandidateAsync(item.Id, new CandidateRequest { Name = item.Name, Profile = item.Profile,
                MunicipalityId = item.MunicipalityId, PositionId = item.PositionId }, item.Revision, new TestImage(Png()));
            item = await data.CandidateAsync(item.Id);
            var name = Path.GetFileName(item.PhotoUrl!);
            await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantQueryContext>().AllowAll();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var foreign = await db.Candidates.FirstAsync(c => c.MunicipalityId != item.MunicipalityId);
            foreign.PhotoFileName = name;
            await db.SaveChangesAsync();
            await data.DeleteCandidateAsync(item.Id, item.Revision);
            Assert.True(File.Exists(Path.Combine(services.GetRequiredService<AdminImageStore>().RootPath, "candidates", name)));
            return true;
        });
    }

    [Fact]
    public async Task ImageValidationRejectsSpoofingOversizeAndExcessiveDimensions()
    {
        await using var factory = new AdminFactory();
        var images = factory.Services.GetRequiredService<AdminImageStore>();
        await Assert.ThrowsAsync<ValidationException>(() => images.SaveAsync(new MemoryStream(Png()), "image.jpg", "image/jpeg", Png().Length, "candidates"));
        await Assert.ThrowsAsync<ValidationException>(() => images.SaveAsync(new MemoryStream(Png()), "image.svg", "image/svg+xml", Png().Length, "candidates"));
        await Assert.ThrowsAsync<ValidationException>(() => images.SaveAsync(new MemoryStream(new byte[AdminImageStore.MaxBytes + 1]), "image.png", "image/png", 1, "candidates"));
        var wide = Png(8001, 1);
        await Assert.ThrowsAsync<ValidationException>(() => images.SaveAsync(new MemoryStream(wide), "image.png", "image/png", wide.Length, "candidates"));
        Assert.Null(images.Url("../escape.png", "candidates"));
        Assert.Null(images.Url(new string('a', 32) + ".png", "other"));
        Assert.Throws<InvalidOperationException>(() => AdminImageStore.ResolveRoot(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "wwwroot")));
    }

    [Fact]
    public async Task FailedMunicipalitySaveDoesNotLeaveUploadedLogo()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Super", async (data, services) =>
        {
            var existing = (await data.MunicipalitiesAsync())[0];
            await Assert.ThrowsAsync<MEMS.Data.Services.RecordConflictException>(() => data.SaveMunicipalityAsync(null,
                new MEMS.Admin.Models.MunicipalityForm { Name = "Duplicate", Slug = existing.Slug, Province = "BC", ElectionDate = new DateOnly(2026, 10, 17) },
                null, new TestImage(Png())));
            Assert.Empty(Directory.GetFiles(Path.Combine(services.GetRequiredService<AdminImageStore>().RootPath, "logos")));
            return true;
        });
    }
}
