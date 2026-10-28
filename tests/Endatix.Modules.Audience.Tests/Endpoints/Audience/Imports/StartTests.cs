using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Endpoints.Audience.Imports;
using Endatix.Modules.Audience.Features.Imports;
using FastEndpoints;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Tests.Endpoints.Audience.Imports;

public sealed class StartTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly Start _endpoint;

    public StartTests()
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(1);
        _endpoint = Factory.Create<Start>(_mediator, tenant, Substitute.For<IUserContext>());
        _mediator.Send(Arg.Any<StartImportCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImportViewDto>.NotFound("Form not found."));
    }

    [Fact]
    public async Task ExecuteAsync_ClearOptions_SendsThemOnTheCommand()
    {
        // Act
        var response = await _endpoint.ExecuteAsync(
            Request("clear", "clear"), TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<StartImportCommand>(command => command.Options == new AudienceImportOptions(true, true)),
            Arg.Any<CancellationToken>());
        (response.Result as ProblemHttpResult)!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ExecuteAsync_NoOptions_KeepsEveryStoredValue()
    {
        // Act
        await _endpoint.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<StartImportCommand>(command => command.Options == AudienceImportOptions.Keep),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("keep", null, true)]
    [InlineData(null, "clear", true)]
    [InlineData("wipe", null, false)]
    [InlineData(null, "Clear", false)]
    public void Validate_Options_AcceptOnlyKeepOrClear(string? emptyCells, string? missingProperties, bool valid)
    {
        // Act
        var result = new StartAudienceImportValidator().Validate(Request(emptyCells, missingProperties));

        // Assert
        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validate_UnknownEmptyCells_NamesTheWireField()
    {
        // Act
        var result = new StartAudienceImportValidator().Validate(Request(emptyCells: "wipe"));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("emptyCells must be keep or clear.");
    }

    private static StartAudienceImportRequest Request(string? emptyCells = null, string? missingProperties = null) => new()
    {
        EmptyCells = emptyCells,
        MissingProperties = missingProperties,
        FormId = 10,
        FileName = "people.csv",
        Headers = ["email"],
        RowCount = 1,
        IdentifierColumn = "email",
    };
}
