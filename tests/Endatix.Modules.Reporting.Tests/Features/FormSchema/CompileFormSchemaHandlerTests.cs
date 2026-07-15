using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Features.FormSchema;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class CompileFormSchemaHandlerTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    [Fact]
    public async Task Handle_WhenFormMissing_ReturnsNotFound()
    {
        IRepository<Form> formsRepository = Substitute.For<IRepository<Form>>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();
        CompileFormSchemaHandler handler = new(formsRepository, schemaProcessor);

        Result<CompileFormSchemaResult> result = await handler.Handle(
            new CompileFormSchemaCommand(FormId, TenantId),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await schemaProcessor.DidNotReceive()
            .ProcessAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActiveDefinitionExists_CompilesSchema()
    {
        Form form = new(TenantId, "Test form") { Id = FormId };
        FormDefinition definition = new(TenantId, isDraft: false, """{"pages":[]}""")
        {
            Id = FormDefinitionId,
        };
        form.AddFormDefinition(definition);
        form.SetActiveFormDefinition(definition);

        IRepository<Form> formsRepository = Substitute.For<IRepository<Form>>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);

        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();
        CompileFormSchemaHandler handler = new(formsRepository, schemaProcessor);

        Result<CompileFormSchemaResult> result = await handler.Handle(
            new CompileFormSchemaCommand(FormId, TenantId),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FormDefinitionId.Should().Be(FormDefinitionId);
        await schemaProcessor.Received(1)
            .ProcessAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>());
    }
}
