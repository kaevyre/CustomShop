using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public sealed class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        // RowVersion у DTO — Base64-рядок; у сутності — byte[]
        CreateMap<byte[], string>().ConvertUsing(bytes => Convert.ToBase64String(bytes));

        CreateMap<Brand, BrandDto>();

        CreateMap<Product, ProductDto>();                         // BrandName ← Brand.Name (flattening)
        CreateMap<Product, ProductDetailsDto>()
            .ForMember(d => d.Categories, o => o.MapFrom(s => s.ProductCategories));
        CreateMap<ProductSpecification, ProductSpecificationDto>();
        CreateMap<CustomizationOption, CustomizationOptionDto>();
        CreateMap<ProductCategory, ProductCategoryDto>();         // CategoryName ← Category.Name

        CreateMap<Category, CategoryDto>();
        CreateMap<Category, CategoryWithProductsDto>()
            .ForMember(d => d.Products, o => o.MapFrom(s => s.ProductCategories));
        CreateMap<ProductCategory, CategoryProductDto>();         // ProductName ← Product.Name, ProductBrandName ← Product.Brand.Name
    }
}
