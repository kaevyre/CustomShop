using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Catalog.Domain.Exceptions;
using Catalog.Grpc;
using Grpc.Core;

namespace Catalog.Api.Grpc;

/// <summary>
/// gRPC-адаптер над BLL (як контролер): викликає той самий IProductService, тож кеш працює для REST і gRPC без дублювання.
/// Доменні винятки перетворюються на RpcException з точним кодом, а не на загальний Internal.
/// </summary>
public sealed class CatalogGrpcService(IProductService products) : CatalogService.CatalogServiceBase
{
    private const int MaxIds = 100;

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            return ToReply(await products.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ProductsReply> GetProductsByIds(GetProductsByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count == 0 || request.Ids.Count > MaxIds)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Ids must contain from 1 to {MaxIds} items"));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every id must be positive"));

        var found = await products.GetByIdsAsync(request.Ids.Distinct().ToList(), context.CancellationToken);

        var reply = new ProductsReply();
        reply.Products.AddRange(found.Select(ToReply));
        return reply;
    }

    private static ProductReply ToReply(ProductDto product)
    {
        var reply = new ProductReply
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            ProductType = product.ProductType,
            Price = ToMoney(product.BasePrice),
            Stock = product.Stock,
            IsCustomizable = product.IsCustomizable,
            IsActive = product.IsActive
        };

        if (product.BrandName is not null)
            reply.BrandName = product.BrandName;
        return reply;
    }

    private static Money ToMoney(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        return new Money { Units = units, Nanos = decimal.ToInt32((value - units) * 1_000_000_000m) };
    }
}
