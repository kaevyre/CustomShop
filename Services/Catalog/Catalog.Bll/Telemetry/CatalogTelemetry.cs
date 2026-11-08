using System.Diagnostics;

namespace Catalog.Bll.Telemetry;

/// <summary>
/// ActivitySource для власних spans бізнес-операцій каталогу (Лаб. 05).
/// ServiceDefaults підписує OpenTelemetry на джерела "Catalog.*", тож spans видно в trace запиту в Dashboard.
/// </summary>
public static class CatalogTelemetry
{
    public const string SourceName = "Catalog.Bll";

    public static readonly ActivitySource Source = new(SourceName);
}
