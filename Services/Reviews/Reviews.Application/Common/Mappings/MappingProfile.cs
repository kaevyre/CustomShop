using AutoMapper;
using MongoDB.Bson;
using Reviews.Application.Builds;
using Reviews.Application.Reviews;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Common.Mappings;

/// <summary>
/// Сутності → DTO: ObjectId → string, Value Objects → примітиви/DTO, вкладені масиви comments[] та items[].
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
        CreateMap<ObjectId?, string?>().ConvertUsing(id => id.HasValue ? id.Value.ToString() : null);
        CreateMap<Rating, int>().ConvertUsing(r => r.Value);

        CreateMap<Author, AuthorDto>();
        CreateMap<Comment, CommentDto>();

        CreateMap<Review, ReviewDetailsDto>()
            .ForMember(d => d.ProductType, o => o.MapFrom(s => s.ProductType.HasValue ? s.ProductType.Value.ToString() : null))
            .ForMember(d => d.Customization, o => o.MapFrom(s => s.Customization.ToDictionary(p => p.Key, p => p.Value)));

        CreateMap<Review, ReviewDto>()
            .ForCtorParam(nameof(ReviewDto.ProductType), o => o.MapFrom(s => s.ProductType.HasValue ? s.ProductType.Value.ToString() : null))
            .ForCtorParam(nameof(ReviewDto.AuthorProfileId), o => o.MapFrom(s => s.Author.ProfileId.ToString()))
            .ForCtorParam(nameof(ReviewDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName))
            .ForCtorParam(nameof(ReviewDto.CommentsCount), o => o.MapFrom(s => s.Comments.Count));

        CreateMap<BuildItem, BuildItemDto>()
            .ForCtorParam(nameof(BuildItemDto.ProductType), o => o.MapFrom(s => s.ProductType.ToString()))
            .ForCtorParam(nameof(BuildItemDto.Customization), o => o.MapFrom(s => s.Customization.ToDictionary(p => p.Key, p => p.Value)));

        CreateMap<Build, BuildDetailsDto>();
    }
}
