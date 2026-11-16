using System.ComponentModel.DataAnnotations;

namespace MEMS.Data.Entities;

public class Municipality
{
    public int MunicipalityId { get; set; }

    [Required, StringLength(100), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    [ConcurrencyCheck]
    [Required, StringLength(32)]
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Province { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime ElectionDate { get; set; }

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(255)]
    public string? LogoFileName { get; set; }

    // Internal sample identifier: keep unchanged when editing seeded records.
    [ScaffoldColumn(false), StringLength(100)]
    public string? SeedKey { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}

