using Orders.Bll.Dtos;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Validation;

internal static class OrderRules
{
    public static void EnsureValid(CreateOrderDto dto)
    {
        var errors = new Dictionary<string, string[]>();

        for (var i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];

            var duplicatedGroups = item.Options
                .GroupBy(o => o.Group.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicatedGroups.Length > 0)
            {
                errors[$"items[{i}].options"] =
                    [$"Option group can be selected only once: {string.Join(", ", duplicatedGroups)}."];
            }
        }

        if (errors.Count > 0)
            throw new DomainValidationException(errors);
    }

    public static void EnsureDiscountWithinPrice(int index, CreateOrderItemDto item, Product product)
    {
        if (item.Discount > product.Price)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                [$"items[{index}].discount"] =
                    [$"Discount ({item.Discount}) cannot exceed the product price ({product.Price})."]
            });
        }
    }
}
