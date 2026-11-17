using System.Diagnostics;

namespace MEMS.Data.Observability;

public static class DataTelemetry
{
    public const string SourceName = "MEMS.Data";
    public static ActivitySource Source { get; } = new(SourceName);
}
