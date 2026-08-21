using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.UpdateDetails;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class PartialUpdateTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdate>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 1, Name = "Cities" };
        _mediator.Send(Arg.Any<UpdateDataListDetailsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid());

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_DataListNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 1, Name = "Cities" };
        _mediator.Send(Arg.Any<UpdateDataListDetailsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Data list not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateName_ReturnsProblemDetails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 1, Name = "Cities" };
        ValidationError duplicateError = new()
        {
            Identifier = nameof(UpdateDataListDetailsCommand.Name),
            ErrorMessage = "A data list with the name 'Cities' already exists.",
            ErrorCode = UpdateDataListDetailsHandler.DuplicateNameErrorCode
        };
        _mediator.Send(Arg.Any<UpdateDataListDetailsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(duplicateError));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithDetails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest
        {
            DataListId = 1,
            Name = "Metros",
            Description = "Updated"
        };
        DataListDto dto = new(
            1,
            "Metros",
            "Updated",
            DateTime.UtcNow,
            DateTime.UtcNow,
            true,
            0,
            "en",
            [],
            []);
        _mediator.Send(Arg.Any<UpdateDataListDetailsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<DataListDetailsModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(1);
        okResult.Value.Name.Should().Be("Metros");
        okResult.Value.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest
        {
            DataListId = 123,
            Name = "Metros",
            Description = "Desc"
        };
        DataListDto dto = new(123, "Metros", "Desc", DateTime.UtcNow, null, true, 0, "en", [], []);
        _mediator.Send(Arg.Any<UpdateDataListDetailsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateDataListDetailsCommand>(cmd =>
                cmd.DataListId == request.DataListId
                && cmd.Name == request.Name
                && cmd.Description == request.Description),
            Arg.Any<CancellationToken>());
    }
}
