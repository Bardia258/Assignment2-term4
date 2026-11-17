using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Contracts;

namespace MEMS.Admin.Models;

public sealed class MunicipalityForm : MunicipalityRequest, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ElectionDate.HasValue && ElectionDate.Value.Year is < 2000 or > 2100)
            yield return new ValidationResult("Choose an election date between 2000 and 2100.", [nameof(ElectionDate)]);
    }
}

public sealed record DashboardCounts(int Municipalities, int Positions, int Candidates);
