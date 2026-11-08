using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Domain.Exceptions;
using Orders.Grpc;

namespace Orders.Api.Grpc;

/// <summary>gRPC-адаптер над BLL (як контролер): перетворює DTO на повідомлення, а доменні винятки — на RpcException.</summary>
public sealed class OrdersGrpcService(IOrderService orders) : OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            return ToReply(await orders.GetAsync(request.Id, context.CancellationToken));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static OrderReply ToReply(OrderDto order)
    {
        var reply = new OrderReply
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status,
            OrderDate = ToTimestamp(order.OrderDate),
            PaidAt = ToTimestamp(order.PaidAt),
            ShippedAt = ToTimestamp(order.ShippedAt),
            TotalAmount = ToMoney(order.TotalAmount)
        };

        // лише ім'я: email і телефон покупця внутрішнім клієнтам не потрібні
        if (order.Customer is { } customer)
            reply.Customer = new CustomerSummary { Id = customer.Id, FullName = customer.FullName };

        if (order.Delivery is { } delivery)
        {
            reply.Delivery = new DeliverySummary
            {
                RecipientName = delivery.RecipientName,
                City = delivery.City,
                DeliveryMethod = delivery.DeliveryMethod
            };
        }

        reply.Items.AddRange(order.Items.Select(ToItem));
        return reply;
    }

    private static OrderItem ToItem(OrderItemDto item)
    {
        var reply = new OrderItem
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            UnitPrice = ToMoney(item.UnitPrice),
            Units = item.Units,
            Discount = ToMoney(item.Discount),
            LineTotal = ToMoney(item.LineTotal)
        };

        reply.Options.AddRange(item.Options.Select(option => new OrderItemOption
        {
            OptionId = option.OptionId,
            OptionGroup = option.OptionGroup,
            OptionValue = option.OptionValue,
            PriceDelta = ToMoney(option.PriceDelta)
        }));
        return reply;
    }

    private static Money ToMoney(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        return new Money { Units = units, Nanos = decimal.ToInt32((value - units) * 1_000_000_000m) };
    }

    private static Timestamp? ToTimestamp(DateTime? value) =>
        value is null ? null : Timestamp.FromDateTime(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
