using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Folders;
using Endatix.Core.UseCases.Folders.GetBySlug;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Api.Tests.Endpoints.Folders;

public class GetBySlugTests
{
    private readonly IMediator _mediator;
    private readonly GetBySlug _endpoint;

    public GetBySlugTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetBySlug>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsErrorResult()
    {
        var request = new GetBySlugRequest { Slug = "test-folder" };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<GetFolderBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        response.Result.Should().NotBeOfType<Ok<FolderModel>>();
    }

    [Fact]
    public async Task ExecuteAsync_FolderNotFound_ReturnsErrorResult()
    {
        var request = new GetBySlugRequest { Slug = "nonexistent-folder" };
        var result = Result.NotFound("Folder not found");

        _mediator.Send(Arg.Any<GetFolderBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        response.Result.Should().NotBeOfType<Ok<FolderModel>>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFolder()
    {
        var request = new GetBySlugRequest { Slug = "test-folder" };
        var folder = new FolderDto { Id = 1, Name = "Test Folder", Slug = "test-folder" };
        var result = Result.Success(folder);

        _mediator.Send(Arg.Any<GetFolderBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var okResult = response.Result as Ok<FolderModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(folder.Id);
        okResult!.Value!.Name.Should().Be(folder.Name);
        okResult!.Value!.Slug.Should().Be(folder.Slug);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        var request = new GetBySlugRequest { Slug = "my-folder" };
        var result = Result.Success(new FolderDto { Id = "1", Name = "My Folder", Slug = "my-folder" });

        _mediator.Send(Arg.Any<GetFolderBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<GetFolderBySlugQuery>(query =>
                query.Slug == request.Slug
            ),
            Arg.Any<CancellationToken>()
        );
    }
}