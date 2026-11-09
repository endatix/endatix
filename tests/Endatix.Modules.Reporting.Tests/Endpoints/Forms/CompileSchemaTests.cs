using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Endpoints.Forms;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class CompileSchemaTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly CompileSchema _endpoint;

    public CompileSchemaTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _authorizationService.ValidateAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _endpoint = Factory.Create<CompileSchema>(_mediator, _tenantContext, _authorizationService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFormNotFound_ReturnsProblemDetails()
    {
        CompileFormSchemaRequest request = new() { FormId = 100 };
        Result<CompileFormSchemaResult> result = Result.NotFound("Form not found.");

        _mediator.Send(Arg.Any<CompileFormSchemaCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsCompiledSchemaSummary()
    {
        const long formId = 100;
        const long formDefinitionId = 200;
        CompileFormSchemaRequest request = new() { FormId = formId };

        CompileFormSchemaResult compileResult = new(formId, formDefinitionId);

        _mediator.Send(Arg.Any<CompileFormSchemaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(compileResult));

        Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<CompileFormSchemaResponse>? ok = response.Result as Ok<CompileFormSchemaResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.FormId.Should().Be(formId);
        ok.Value.FormDefinitionId.Should().Be(formDefinitionId);

        await _mediator.Received(1).Send(
            Arg.Is<CompileFormSchemaCommand>(command =>
                command.FormId == formId &&
                command.TenantId == SampleData.TENANT_ID &&
                command.Replace == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplaceTrue_PassesReplaceToCommand()
    {
        const long formId = 100;
        const long formDefinitionId = 200;
        CompileFormSchemaRequest request = new() { FormId = formId, Replace = true };

        _mediator.Send(Arg.Any<CompileFormSchemaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CompileFormSchemaResult(formId, formDefinitionId)));

        Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<CompileFormSchemaResponse>? ok = response.Result as Ok<CompileFormSchemaResponse>;
        ok.Should().NotBeNull();

        await _mediator.Received(1).Send(
            Arg.Is<CompileFormSchemaCommand>(command =>
                command.FormId == formId &&
                command.TenantId == SampleData.TENANT_ID &&
                command.Replace),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplaceTrueWithoutFormEdit_ReturnsForbiddenAndSendsNothing()
    {
        CompileFormSchemaRequest request = new() { FormId = 100, Replace = true };
        _authorizationService.ValidateAccessAsync(Actions.Forms.Edit, Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Permission 'forms.edit' required to access this resource."));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        await _mediator.DidNotReceive().Send(Arg.Any<CompileFormSchemaCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplaceFalse_DoesNotCheckFormEdit()
    {
        CompileFormSchemaRequest request = new() { FormId = 100 };
        _mediator.Send(Arg.Any<CompileFormSchemaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Form not found."));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _authorizationService.DidNotReceive()
            .ValidateAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
