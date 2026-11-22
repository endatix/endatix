using Endatix.Api.Endpoints.FormSettings;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormSettings;
using Endatix.Core.UseCases.FormSettings.Get;
using Endatix.Core.UseCases.FormSettings.PartialUpdate;
using FastEndpoints;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.FormSettings;

public sealed class FormSettingsEndpointsTests
{
    private const long FORM_ID = 42;

    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ReturnsTheSettingsWithTheirETag()
    {
        // Arrange
        var endpoint = Factory.Create<Get>(_mediator);
        _mediator.Send(new GetFormSettingsQuery(FORM_ID), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new FormSettingsDto(3, new PdfSettings { UsesFormTheme = true })));

        // Act
        var response = await endpoint.ExecuteAsync(new GetFormSettingsRequest { FormId = FORM_ID }, Cancellation);

        // Assert
        var model = response.Result.As<Ok<FormSettingsModel>>().Value!;
        model.Version.Should().Be(3);
        model.Pdf.UsesFormTheme.Should().BeTrue();
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"3\"");
    }

    [Fact]
    public async Task Get_MissingForm_IsNotFound()
    {
        // Arrange
        var endpoint = Factory.Create<Get>(_mediator);
        _mediator.Send(Arg.Any<GetFormSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FormSettingsDto>.NotFound("Form not found."));

        // Act
        var response = await endpoint.ExecuteAsync(new GetFormSettingsRequest { FormId = FORM_ID }, Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetValidator_RefusesAFormIdThatIsNotPositive(long formId)
    {
        var result = new GetFormSettingsValidator().Validate(new GetFormSettingsRequest { FormId = formId });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, StatusCodes.Status428PreconditionRequired)]
    [InlineData("W/\"0\"", StatusCodes.Status400BadRequest)]
    public async Task PartialUpdate_WithoutAReadableIfMatch_IsRefusedWithoutSending(string? ifMatch, int status)
    {
        // Arrange
        var endpoint = PartialUpdateEndpoint(ifMatch);

        // Act
        var response = await endpoint.ExecuteAsync(ThemeOn(), Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(status);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(PartialUpdateFormSettingsCommand)!, Cancellation);
    }

    [Fact]
    public async Task PartialUpdate_SendsTheVersionAndSectionAndReturnsTheNewETag()
    {
        // Arrange
        var endpoint = PartialUpdateEndpoint("\"2\"");
        _mediator.Send(Arg.Any<PartialUpdateFormSettingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new FormSettingsDto(3, new PdfSettings { UsesFormTheme = true })));

        // Act
        var response = await endpoint.ExecuteAsync(ThemeOn(), Cancellation);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateFormSettingsCommand>(command =>
                command.FormId == FORM_ID && command.ExpectedVersion == 2 && command.Change.Pdf!.UsesFormTheme),
            Arg.Any<CancellationToken>());
        response.Result.As<Ok<FormSettingsModel>>().Value!.Version.Should().Be(3);
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"3\"");
    }

    [Fact]
    public async Task PartialUpdate_Stale_IsPreconditionFailedWithTheCurrentVersion()
    {
        // Arrange
        var endpoint = PartialUpdateEndpoint("\"0\"");
        _mediator.Send(Arg.Any<PartialUpdateFormSettingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FormSettingsDto>.Conflict("The form settings changed since they were read."));
        _mediator.Send(new GetFormSettingsQuery(FORM_ID), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new FormSettingsDto(1, new PdfSettings())));

        // Act
        var response = await endpoint.ExecuteAsync(ThemeOn(), Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
        problem.ProblemDetails.Extensions["version"].Should().Be("1");
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"1\"");
    }

    [Fact]
    public async Task PartialUpdate_MissingForm_IsNotFound()
    {
        // Arrange
        var endpoint = PartialUpdateEndpoint("\"0\"");
        _mediator.Send(Arg.Any<PartialUpdateFormSettingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FormSettingsDto>.NotFound("Form not found."));

        // Act
        var response = await endpoint.ExecuteAsync(ThemeOn(), Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private PartialUpdate PartialUpdateEndpoint(string? ifMatch) =>
        Factory.Create<PartialUpdate>(context =>
        {
            if (ifMatch is not null)
            {
                context.Request.Headers.IfMatch = ifMatch;
            }
        }, _mediator);

    private static PartialUpdateFormSettingsRequest ThemeOn() =>
        new() { FormId = FORM_ID, Pdf = new PdfSettingsModel { UsesFormTheme = true } };
}
