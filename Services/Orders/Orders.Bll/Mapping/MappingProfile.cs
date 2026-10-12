using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Domain.Models;

namespace Orders.Bll.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<byte[], string>().ConvertUsing(bytes => Convert.ToBase64String(bytes));

        CreateMap<Customer, CustomerDto>();
        CreateMap<Customer, CustomerSummaryDto>();
        CreateMap<CreateCustomerDto, Customer>();
        CreateMap<UpdateCustomerDto, Customer>()
            .ForMember(d => d.RowVersion, o => o.Ignore());   // розбирається в RequestRules.ParseRowVersion

        CreateMap<Product, ProductDto>();
        CreateMap<CreateProductDto, Product>()
            .ForMember(d => d.RowVersion, o => o.Ignore());
        CreateMap<UpdateProductDto, Product>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<OrderDetails, DeliveryDto>().ReverseMap();

        CreateMap<CreateOrderItemOptionDto, OrderItemOption>()
            .ForMember(d => d.OptionGroup, o => o.MapFrom(s => s.Group.Trim()))
            .ForMember(d => d.OptionValue, o => o.MapFrom(s => s.Value.Trim()));
        CreateMap<OrderItemOption, OrderItemOptionDto>();
        CreateMap<OrderItem, OrderItemDto>();

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Delivery, o => o.MapFrom(s => s.Details));

        CreateMap<OrderSummary, OrderSummaryDto>();
        CreateMap<OrderStatusResult, OrderStatusResultDto>();
        CreateMap<OrderStatusStatistics, OrderStatusStatisticsDto>();
    }
}
