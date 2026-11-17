using MEMS.Shared.Contracts;

namespace MEMS.Admin.Models;

public sealed class MunicipalityForm : MunicipalityRequest;

public sealed record DashboardCounts(int Municipalities, int Positions, int Candidates);
