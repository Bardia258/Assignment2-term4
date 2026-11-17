using MEMS.Data.Entities;
using MEMS.Shared.Contracts;

namespace MEMS.Api.Infrastructure;

public sealed class DirectoryMapper(IConfiguration configuration)
{
    private string? Image(string? file, string folder)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        var path = "/uploads/" + folder + "/" + Uri.EscapeDataString(Path.GetFileName(file));
        var baseUrl = configuration["Images:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl.TrimEnd('/') + path;
    }

    public MunicipalityResponse Municipality(Municipality m) => new(m.MunicipalityId, m.Name, m.Slug,
        m.Province, m.ElectionDate, m.Description, Image(m.LogoFileName, "logos"), m.Revision);
    public PositionResponse Position(Position p) => new(p.PositionId, p.Name, p.SeatsAvailable,
        p.MunicipalityId, p.Municipality.Name, p.Revision);
    public CandidateResponse Candidate(Candidate c) => new(c.CandidateId, c.Name, c.Profile,
        Image(c.PhotoFileName, "candidates"), c.Website, c.Email, c.DateCreated, c.MunicipalityId,
        c.Municipality.Name, c.PositionId, c.Position.Name, Municipality(c.Municipality),
        new PositionResponse(c.PositionId, c.Position.Name, c.Position.SeatsAvailable, c.MunicipalityId,
            c.Municipality.Name, c.Position.Revision), c.Revision);
}
