using System.ComponentModel.DataAnnotations;
using MEMS.Admin.Security;
using MEMS.Admin.Services;
using MEMS.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Admin.Components.Shared;

public abstract class AdminPageBase : ComponentBase
{
    [Inject] protected AdminDataService Data { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<AdminPageBase> Logger { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "saved")] public bool Saved { get; set; }
    [SupplyParameterFromQuery(Name = "deleted")] public bool Deleted { get; set; }
    protected bool Busy { get; set; }
    protected bool Loaded { get; set; }
    protected bool InteractiveReady { get; private set; }
    protected string? Error { get; set; }
    protected AdminSession? Session { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender) { InteractiveReady = true; StateHasChanged(); }
    }

    protected async Task Run(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; Error = null;
        try { await action(); }
        catch (Exception error)
        {
            Logger.LogWarning("Admin operation failed with {ErrorType}", error.GetType().Name);
            Error = error switch
            {
                ValidationException => error.Message,
                RecordConflictException => error.Message,
                KeyNotFoundException => "This record was not found in your permitted municipality.",
                UnauthorizedAccessException => "Your session expired or this operation is outside your permissions. Sign in again if your assignment changed.",
                DbUpdateConcurrencyException => "This record changed. Reload it before saving or deleting.",
                DbUpdateException => "The change conflicts with existing or related records. Reload and check your entries.",
                _ => "The operation could not be completed. Please retry or contact an administrator."
            };
        }
        finally { Busy = false; Loaded = true; }
    }
}
