using AutoMapper;
using Reviews.Application.Builds.Commands.UpdateBuild;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Mapping;

/// <summary>HTTP-контракти → команди MediatR (Id і очікувана версія додаються в контролері).</summary>
public sealed class ApiMappingProfile : Profile
{
    public ApiMappingProfile()
    {
        CreateMap<UpdateReviewRequest, UpdateReviewCommand>()
            .ForMember(c => c.Id, o => o.Ignore())
            .ForMember(c => c.ExpectedVersion, o => o.MapFrom(r => r.Version));

        CreateMap<UpdateBuildRequest, UpdateBuildCommand>()
            .ForMember(c => c.Id, o => o.Ignore())
            .ForMember(c => c.ExpectedVersion, o => o.MapFrom(r => r.Version));
    }
}
