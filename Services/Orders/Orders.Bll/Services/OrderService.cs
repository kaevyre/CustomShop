using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Caching;
using Orders.Bll.Dtos;
using Orders.Bll.Telemetry;
using Orders.Bll.Validation;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class OrderService : IOrderService
{
    private const string Actor = "orders-api";

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;
    private readonly ITwoLevelCache _cache;

    public OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger, ITwoLevelCache cache)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
    }

    public async Task<OrderDto> GetAsync(long id, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(Order), id);

        order.Items = (await _uow.OrderItems.GetByOrderIdAsync(id, ct)).ToList();
        return _mapper.Map<OrderDto>(order);
    }

    public async Task<PagedResultDto<OrderSummaryDto>> GetCustomerOrdersAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        RequestRules.EnsurePaging(page, pageSize);

        if (status is not null && !OrderStatus.IsValid(status))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = [$"Unknown status '{status}'. Allowed: {string.Join(", ", OrderStatus.All)}."]
            });
        }

        if (await _uow.Customers.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException(nameof(Customer), customerId);

        var result = await _uow.OrderSummaries.GetByCustomerAsync(customerId, status, page, pageSize, ct);
        return new PagedResultDto<OrderSummaryDto>
        {
            Items = _mapper.Map<List<OrderSummaryDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<OrderDto> PlaceAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        using var activity = OrdersTelemetry.Source.StartActivity("PlaceOrder");
        activity?.SetTag("customer.id", dto.CustomerId);
        activity?.SetTag("order.items", dto.Items.Count);

        // Рівень 2 валідації (рівень 1 — атрибути DataAnnotations на DTO, їх перевіряє [ApiController])
        OrderRules.EnsureValid(dto);

        // Усі кроки — одна транзакція на одному з'єднанні: або замовлення з усіма позиціями, або нічого.
        var orderId = await _uow.ExecuteInTransactionAsync(async () =>
        {
            if (await _uow.Customers.GetByIdAsync(dto.CustomerId, ct) is null)
                throw new NotFoundException(nameof(Customer), dto.CustomerId);

            var products = (await _uow.Products.GetByIdsAsync(dto.Items.Select(i => i.ProductId), ct))
                .ToDictionary(p => p.Id);

            EnsureProductsAvailable(dto, products);

            var order = new Order
            {
                CustomerId = dto.CustomerId,
                CreatedBy = Actor,
                Details = _mapper.Map<OrderDetails>(dto.Delivery)
            };
            var newOrderId = await _uow.Orders.CreateWithDetailsAsync(order, ct);

            // 4. позиції (репозиторій позицій). Назва й ціна — ЗНІМОК із довідника на цей момент:
            //    далі вони не змінюються, навіть якщо товар подорожчає чи його перейменують.
            foreach (var line in dto.Items)
            {
                var product = products[line.ProductId];

                var item = new OrderItem
                {
                    OrderId = newOrderId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Units = line.Units,
                    Discount = line.Discount,
                    Options = _mapper.Map<List<OrderItemOption>>(line.Options)
                };

                await _uow.OrderItems.AddWithOptionsAsync(item, ct);
            }

            return newOrderId;
        }, IsolationLevel.ReadCommitted, ct);

        await _cache.InvalidateAsync("OrderPlaced", OrdersCacheKeys.Statistics);

        var placed = await GetAsync(orderId, ct);
        activity?.SetTag("order.id", orderId);
        _logger.LogInformation(
            "Order {OrderId} placed by customer {CustomerId}: {ItemsCount} items, amount {Amount}, status {Status}",
            orderId, dto.CustomerId, placed.Items.Count, placed.TotalAmount, placed.Status);
        return placed;
    }

    public async Task<OrderStatusResultDto> ChangeStatusAsync(
        long id, ChangeOrderStatusDto dto, CancellationToken ct = default)
    {
        if (!OrderStatus.IsValid(dto.NewStatus))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["newStatus"] = [$"Unknown status '{dto.NewStatus}'. Allowed: {string.Join(", ", OrderStatus.All)}."]
            });
        }

        using var activity = OrdersTelemetry.Source.StartActivity("ChangeOrderStatus");
        activity?.SetTag("order.id", id);
        activity?.SetTag("order.status.new", dto.NewStatus);

        var expectedRowVersion = RequestRules.ParseRowVersion(dto.RowVersion);

        var order = await _uow.Orders.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Order), id);

        if (!OrderStatus.CanTransition(order.Status, dto.NewStatus))
        {
            _logger.LogWarning("Order {OrderId}: transition {OldStatus} -> {Status} rejected",
                id, order.Status, dto.NewStatus);
            throw new BusinessConflictException(
                $"Transition {order.Status} -> {dto.NewStatus} is not allowed. " +
                $"Allowed from {order.Status}: {FormatNext(order.Status)}.");
        }

        var result = await _uow.Orders.ChangeStatusAsync(id, dto.NewStatus, expectedRowVersion, Actor, ct);
        await InvalidateAfterStatusChangeAsync(id, dto.NewStatus, ct);
        _logger.LogInformation("Order {OrderId} status changed {OldStatus} -> {Status}",
            id, order.Status, result.Status);
        return _mapper.Map<OrderStatusResultDto>(result);
    }

    public async Task<IReadOnlyList<OrderStatusStatisticsDto>> GetStatusStatisticsAsync(CancellationToken ct = default) =>
        (await _cache.GetOrCreateAsync<List<OrderStatusStatisticsDto>>(OrdersCacheKeys.Statistics, OrdersCacheProfiles.Statistics,
            async cancellation =>
            {
                var rows = await _uow.OrderSummaries.GetStatusStatisticsAsync(cancellation);
                return _mapper.Map<List<OrderStatusStatisticsDto>>(rows);
            }, ct))!;

    /// <summary>
    /// Статистика й агрегована картка змінюються завжди; залишки товарів — лише коли usp_ChangeOrderStatus
    /// резервує склад (StockConfirmed) або повертає його (Cancelled).
    /// </summary>
    private async Task InvalidateAfterStatusChangeAsync(long orderId, string newStatus, CancellationToken ct)
    {
        const string reason = "OrderStatusChanged";
        await _cache.InvalidateAsync(reason, OrdersCacheKeys.Statistics, OrdersCacheKeys.AggregatedOrder(orderId));

        if (newStatus is not (OrderStatus.StockConfirmed or OrderStatus.Cancelled))
            return;

        var items = await _uow.OrderItems.GetByOrderIdAsync(orderId, ct);
        await _cache.InvalidateAsync(reason, items.Select(i => OrdersCacheKeys.Product(i.ProductId)).Distinct().ToArray());
        await _cache.BumpVersionAsync(OrdersCacheKeys.ProductListsVersion, reason);
    }

    private static void EnsureProductsAvailable(CreateOrderDto dto, IReadOnlyDictionary<long, Product> products)
    {
        for (var i = 0; i < dto.Items.Count; i++)
        {
            var line = dto.Items[i];

            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException(nameof(Product), line.ProductId);

            if (!product.IsActive)
                throw new BusinessConflictException($"Product '{product.Name}' (id {product.Id}) is not available.");

            OrderRules.EnsureDiscountWithinPrice(i, line, product);
        }

        foreach (var group in dto.Items.GroupBy(i => i.ProductId))
        {
            var product = products[group.Key];
            var requested = group.Sum(i => i.Units);
            if (requested > product.Stock)
            {
                throw new BusinessConflictException(
                    $"Insufficient stock for '{product.Name}': requested {requested}, available {product.Stock}.");
            }
        }
    }

    private static string FormatNext(string status)
    {
        var next = OrderStatus.NextStatuses(status);
        return next.Count == 0 ? "none (final status)" : string.Join(", ", next);
    }
}
