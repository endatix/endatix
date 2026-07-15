using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public sealed class BackfillSubmissionsHandlerTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    [Fact]
    public async Task Handle_WhenFormMissing_ReturnsNotFound()
    {
        IRepository<FormDefinition> formDefinitionsRepository = Substitute.For<IRepository<FormDefinition>>();
        formDefinitionsRepository
            .AnyAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

        ISubmissionBackfillProcessor backfillProcessor = Substitute.For<ISubmissionBackfillProcessor>();
        BackfillSubmissionsHandler handler = new(formDefinitionsRepository, backfillProcessor);

        Result<SubmissionBackfillResult> result = await handler.Handle(
            new BackfillSubmissionsCommand(FormId, TenantId),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await backfillProcessor.DidNotReceive()
            .BackfillFormAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<SubmissionBackfillOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFormExists_DelegatesToBackfillProcessor()
    {
        SubmissionBackfillResult backfillResult = new(
            FormId,
            Scanned: 2,
            Processed: 1,
            Skipped: 1,
            Failed: 0,
            HasMore: false,
            NextAfterSubmissionId: null,
            FailedSubmissionIds: []);

        IRepository<FormDefinition> formDefinitionsRepository = Substitute.For<IRepository<FormDefinition>>();
        formDefinitionsRepository
            .AnyAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        ISubmissionBackfillProcessor backfillProcessor = Substitute.For<ISubmissionBackfillProcessor>();
        backfillProcessor
            .BackfillFormAsync(
                TenantId,
                FormId,
                Arg.Is<SubmissionBackfillOptions>(options => options.BatchSize == 50 && options.Force),
                Arg.Any<CancellationToken>())
            .Returns(backfillResult);

        BackfillSubmissionsHandler handler = new(formDefinitionsRepository, backfillProcessor);

        Result<SubmissionBackfillResult> result = await handler.Handle(
            new BackfillSubmissionsCommand(FormId, TenantId, BatchSize: 50, Force: true),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(backfillResult);
    }
}
