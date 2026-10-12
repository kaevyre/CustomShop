namespace Orders.Domain.Models;

public static class OrderStatus
{
    public const string Submitted = "Submitted";
    public const string AwaitingValidation = "AwaitingValidation";
    public const string StockConfirmed = "StockConfirmed";
    public const string Paid = "Paid";
    public const string InProduction = "InProduction";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [Submitted, AwaitingValidation, StockConfirmed, Paid, InProduction, Shipped, Cancelled];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Submitted] = [AwaitingValidation, Cancelled],
            [AwaitingValidation] = [StockConfirmed, Cancelled],
            [StockConfirmed] = [Paid, Cancelled],
            [Paid] = [InProduction],
            [InProduction] = [Shipped],
            [Shipped] = [],
            [Cancelled] = []
        };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static IReadOnlyList<string> NextStatuses(string status) =>
        Transitions.TryGetValue(status, out var next) ? next : [];

    public static bool CanTransition(string from, string to) => NextStatuses(from).Contains(to);
}
