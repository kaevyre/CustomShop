using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Builds;
using Reviews.Application.Builds.Commands.CreateBuild;
using Reviews.Application.Builds.Commands.DeleteBuild;
using Reviews.Application.Builds.Commands.LikeBuild;
using Reviews.Application.Builds.Commands.UpdateBuild;
using Reviews.Application.Builds.Queries;
using Reviews.Application.Common.Models;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;

[Route("api/builds")]
public sealed class BuildsController : ApiControllerBase
{
    /// <summary>Сетапи за тегом або з товаром productId, популярні першими.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<BuildDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BuildDto>>> GetList(
        [FromQuery] string? tag, [FromQuery] long? productId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetBuildsListQuery(tag, productId, page, pageSize), ct));

    [HttpGet("{id}")]
    [ProducesResponseType<BuildDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BuildDetailsDto>> GetById(string id, CancellationToken ct)
    {
        var build = await Mediator.Send(new GetBuildByIdQuery(id), ct);
        SetETag(build.Version);
        return Ok(build);
    }

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateBuildCommand command, CancellationToken ct)
    {
        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    [HttpPut("{id}")]
    [ProducesResponseType<BuildDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BuildDetailsDto>> Update(string id, UpdateBuildRequest request, CancellationToken ct)
    {
        var command = Mapper.Map<UpdateBuildCommand>(request) with
        {
            Id = id,
            ExpectedVersion = ExpectedVersion(request.Version)
        };
        var build = await Mediator.Send(command, ct);
        SetETag(build.Version);
        return Ok(build);
    }

    [HttpPost("{id}/likes")]
    [ProducesResponseType<LikesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LikesResponse>> Like(string id, CancellationToken ct)
        => Ok(new LikesResponse(await Mediator.Send(new LikeBuildCommand(id), ct)));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBuildCommand(id, ExpectedVersion()), ct);
        return NoContent();
    }
}
