using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Platform.EventBus;

/// <summary>
/// CorrelationId поточного HTTP-запиту: заголовок X-Correlation-Id (його виставляє CorrelationIdMiddleware із ServiceDefaults);
/// поза запитом — тег correlation.id найближчого Activity (напр., у consumer, де він відновлений із повідомлення).
/// </summary>
public sealed class CorrelationIdProvider(IHttpContextAccessor accessor) : ICorrelationIdProvider
{
    public const string HeaderName = "X-Correlation-Id";
    public const string MessageHeader = "x-correlation-id";
    public const string ActivityTag = "correlation.id";

    public string? Current
    {
        get
        {
            var fromRequest = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(fromRequest))
                return fromRequest;

            for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
                if (activity.GetTagItem(ActivityTag) is string id && id.Length > 0)
                    return id;

            return null;
        }
    }
}
