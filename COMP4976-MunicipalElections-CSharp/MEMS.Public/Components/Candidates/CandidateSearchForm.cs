using System.ComponentModel.DataAnnotations;

namespace MEMS.Public.Components.Candidates;

/// <summary>
/// Model for the candidate name search box. Same limit as the API's <see cref="CandidateQuery.Name"/>,
/// so a search the page accepts is always one the real API would accept too.
/// </summary>
public sealed class CandidateSearchForm
{
    [StringLength(CandidateFilterState.MaxSearchLength,
        ErrorMessage = "Search can be at most {1} characters. Try part of a first or last name.")]
    public string? Search { get; set; }
}
