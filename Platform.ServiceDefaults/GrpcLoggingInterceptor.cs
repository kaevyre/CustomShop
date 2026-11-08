using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Серверний interceptor gRPC (Лаб. 06): метод, адреса клієнта (peer), статус і тривалість кожного виклику —
/// структурованими властивостями; атрибути rpc.* у поточному span; властивість Transport=grpc у LogContext,
/// тож записи кешу всередині виклику відрізняються від HTTP-запитів.
/// Підключається у сервісі: <c>AddGrpc(o => o.Interceptors.Add&lt;GrpcLoggingInterceptor&gt;())</c>.
/// </summary>
public sealed class GrpcLoggingInterceptor(ILoggerFactory loggerFactory) : Interceptor
{
    // власна категорія: сервіси глушать "Microsoft" до Warning у своїх appsettings
    private readonly ILogger logger = loggerFactory.CreateLogger("Platform.Grpc");

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var (service, method) = ParseMethod(context.Method);
        var activity = Activity.Current;
        activity?.SetTag("rpc.system", "grpc");
        activity?.SetTag("rpc.service", service);
        activity?.SetTag("rpc.method", method);

        var sw = Stopwatch.StartNew();
        using (LogContext.PushProperty("Transport", "grpc"))
        {
            logger.LogDebug("gRPC call started {GrpcMethod} from {Peer}", context.Method, context.Peer);
            try
            {
                var response = await continuation(request, context);

                activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.OK);
                logger.LogInformation("gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms",
                    context.Method, context.Peer, StatusCode.OK, sw.ElapsedMilliseconds);
                return response;
            }
            catch (RpcException ex)
            {
                activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);
                logger.LogWarning(ex, "gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms, detail {GrpcDetail}",
                    context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds, ex.Status.Detail);
                throw;
            }
            catch (Exception ex)
            {
                activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.Unknown);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "gRPC {GrpcMethod} from {Peer}: unhandled {ExceptionType} in {ElapsedMs} ms",
                    context.Method, context.Peer, ex.GetType().Name, sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // context.Method має вигляд "/catalog.CatalogService/GetProduct"
    internal static (string Service, string Method) ParseMethod(string fullMethod)
    {
        var parts = fullMethod.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (fullMethod, fullMethod);
    }
}
