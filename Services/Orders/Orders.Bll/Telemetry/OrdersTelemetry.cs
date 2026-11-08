using System.Diagnostics;

namespace Orders.Bll.Telemetry;

/// <summary>
/// ActivitySource для власних spans бізнес-операцій сервісу замовлень (Лаб. 05).
/// ServiceDefaults підписує OpenTelemetry на джерела "Orders.*", тож spans видно в trace запиту в Dashboard.
/// </summary>
public static class OrdersTelemetry
{
    public const string SourceName = "Orders.Bll";

    public static readonly ActivitySource Source = new(SourceName);
}
