using System.Diagnostics;

namespace Reviews.Application.Common.Telemetry;

/// <summary>
/// ActivitySource для власних spans бізнес-операцій сервісу відгуків (Лаб. 05).
/// ServiceDefaults підписує OpenTelemetry на джерела "Reviews.*", тож spans видно в trace запиту в Dashboard.
/// </summary>
public static class ReviewsTelemetry
{
    public const string SourceName = "Reviews.Application";

    public static readonly ActivitySource Source = new(SourceName);
}
