using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Endpoints.Forms;
using Endatix.Modules.Reporting.Features.SubmissionViews;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class SubmissionViewEndpointsTests
{
    private const long FormId = 100;
    private const string Settings = """{"v":1,"columns":[{"key":"q1","width":120}],"pageSize":25}""";

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();

    public SubmissionViewEndpointsTests()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _userContext.GetCurrentUserId().Returns("7");
    }

    [Fact]
    public async Task Get_FormNotFound_Returns404()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetSubmissionViewQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Form not found."));
        var endpoint = Factory.Create<GetSubmissionView>(_mediator, _tenantContext);

        // Act
        var response = await endpoint.ExecuteAsync(new() { FormId = FormId }, TestContext.Current.CancellationToken);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Get_SavedView_ReturnsRevisionAndSettingsAsJson()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetSubmissionViewQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SubmissionViewDto(3, Settings)));
        var endpoint = Factory.Create<GetSubmissionView>(_mediator, _tenantContext);

        // Act
        var response = await endpoint.ExecuteAsync(new() { FormId = FormId }, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result as Ok<SubmissionViewResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Revision.Should().Be(3);
        ok.Value.Settings.GetRawText().Should().Be(Settings);
        await _mediator.Received(1).Send(
            Arg.Is<GetSubmissionViewQuery>(query => query.TenantId == SampleData.TENANT_ID && query.FormId == FormId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_SendsTheSettingsAsWrittenWithTenantAndUser()
    {
        // Arrange
        _mediator.Send(Arg.Any<SaveSubmissionViewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SubmissionViewDto(2, Settings)));
        var endpoint = Factory.Create<SaveSubmissionView>(_mediator, _tenantContext, _userContext);
        SaveSubmissionViewRequest request = new() { FormId = FormId, Revision = 1, Settings = Parse(Settings) };

        // Act
        var response = await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        (response.Result as Ok<SubmissionViewResponse>)!.Value!.Revision.Should().Be(2);
        await _mediator.Received(1).Send(
            Arg.Is<SaveSubmissionViewCommand>(command =>
                command.Save == new Data.SubmissionViewSave(SampleData.TENANT_ID, FormId, 1, Settings, 7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_StaleRevision_Returns409()
    {
        // Arrange
        _mediator.Send(Arg.Any<SaveSubmissionViewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Conflict(SaveSubmissionViewHandler.ConflictMessage));
        var endpoint = Factory.Create<SaveSubmissionView>(_mediator, _tenantContext, _userContext);
        SaveSubmissionViewRequest request = new() { FormId = FormId, Revision = 1, Settings = Parse(Settings) };

        // Act
        var response = await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Theory]
    [InlineData("""{"v":1,"columns":[]}""", true)]
    [InlineData("""{"v":1,"columns":[{"key":"q1","width":-5}]}""", false)]
    [InlineData("""["not","an","object"]""", false)]
    public void SaveValidator_ChecksSettingsShape(string settings, bool expectedValid)
    {
        // Arrange
        SaveSubmissionViewValidator validator = new();
        SaveSubmissionViewRequest request = new() { FormId = FormId, Revision = 0, Settings = Parse(settings) };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void SaveValidator_MissingSettings_ReportsSettingsRequired()
    {
        // Arrange
        SaveSubmissionViewValidator validator = new();
        SaveSubmissionViewRequest request = new() { FormId = FormId, Revision = 0 };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Settings are required.");
    }

    [Fact]
    public void SaveValidator_OversizedSettings_ReportsSizeCap()
    {
        // Arrange
        SaveSubmissionViewValidator validator = new();
        var padding = new string(' ', SubmissionViewSettings.MaxJsonLength);
        SaveSubmissionViewRequest request = new()
        {
            FormId = FormId,
            Settings = Parse($$"""{"v":1,"columns":[]{{padding}}}"""),
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("cannot be longer than");
    }

    [Fact]
    public void SaveValidator_NegativeRevision_IsInvalid()
    {
        // Arrange
        SaveSubmissionViewValidator validator = new();
        SaveSubmissionViewRequest request = new() { FormId = FormId, Revision = -1, Settings = Parse(SubmissionViewSettings.DefaultJson) };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
