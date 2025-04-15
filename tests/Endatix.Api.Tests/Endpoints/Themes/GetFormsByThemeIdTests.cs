using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Themes;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Themes.GetFormsByThemeId;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class GetFormsByThemeIdTests
{
    private readonly IMediator _mediator;
    private readonly GetFormsByThemeId _endpoint;

    public GetFormsByThemeIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetFormsByThemeId>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new GetFormsByThemeIdRequest { ThemeId = 1 };
        var result = Result<List<Form>>.NotFound();

        _mediator.Send(Arg.Any<GetFormsByThemeIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new GetFormsByThemeIdRequest { ThemeId = 0 }; // Invalid ID
        var result = Result<List<Form>>.Invalid();

        _mediator.Send(Arg.Any<GetFormsByThemeIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormsFound_ReturnsOkWithForms()
    {
        // Arrange
        var request = new GetFormsByThemeIdRequest { ThemeId = 1 };

        var forms = new List<Form>
        {
            new Form(SampleData.TENANT_ID, "Form 1") { Id = 1 },
            new Form(SampleData.TENANT_ID, "Form 2") { Id = 2 }
        };

        var result = Result<List<Form>>.Success(forms);

        _mediator.Send(Arg.Any<GetFormsByThemeIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<FormModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_NoFormsFound_ReturnsOkWithEmptyList()
    {
        // Arrange
        var request = new GetFormsByThemeIdRequest { ThemeId = 1 };
        var forms = new List<Form>();
        var result = Result<List<Form>>.Success(forms);

        _mediator.Send(Arg.Any<GetFormsByThemeIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<FormModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetFormsByThemeIdRequest { ThemeId = 1 };
        var forms = new List<Form>();
        var result = Result<List<Form>>.Success(forms);

        _mediator.Send(Arg.Any<GetFormsByThemeIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetFormsByThemeIdQuery>(query => query.ThemeId == request.ThemeId),
            Arg.Any<CancellationToken>()
        );
    }
}