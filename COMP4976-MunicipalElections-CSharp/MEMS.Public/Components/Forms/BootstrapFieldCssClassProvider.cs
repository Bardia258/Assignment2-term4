using Microsoft.AspNetCore.Components.Forms;

namespace MEMS.Public.Components.Forms;

/// <summary>
/// Maps Blazor's validation state to Bootstrap's <c>is-invalid</c> class (Blazor's defaults,
/// "valid modified"/"invalid", have no Bootstrap styling). No "is-valid" green: a search box that
/// turns green on every keystroke is noise.
/// </summary>
public sealed class BootstrapFieldCssClassProvider : FieldCssClassProvider
{
    public override string GetFieldCssClass(EditContext editContext, in FieldIdentifier fieldIdentifier) =>
        editContext.GetValidationMessages(fieldIdentifier).Any() ? "is-invalid" : "";
}
