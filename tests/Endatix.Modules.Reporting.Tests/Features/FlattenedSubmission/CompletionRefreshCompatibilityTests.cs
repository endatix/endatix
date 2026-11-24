using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public sealed class CompletionRefreshCompatibilityTests
{
    [Fact]
    public async Task RefreshCompletedAsync_ProcessorImplementsOnlyProcessAsync_DeliversCompletionWithItsCancellationToken()
    {
        // Arrange: a host can supply its own implementation of the original public processor interface.
        var work = Substitute.For<ISubmissionFlatteningProcessor>();
        ISubmissionFlatteningProcessor legacy = new LegacyProcessor(work);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await legacy.RefreshCompletedAsync(new FlattenedSubmissionKey(11, 22, 33), ct);

        // Assert
        await work.Received(1).ProcessAsync(11, 22, 33, ct, includeIncomplete: false);
    }

    private sealed class LegacyProcessor(ISubmissionFlatteningProcessor work) : ISubmissionFlatteningProcessor
    {
        public Task ProcessAsync(long tenantId, long formId, long submissionId, CancellationToken cancellationToken,
            bool includeIncomplete = false) =>
            work.ProcessAsync(tenantId, formId, submissionId, cancellationToken, includeIncomplete);
    }

    [Fact]
    public async Task TryBackfillAsync_LegacyProcessor_PreservesItsSuccessfulReturn()
    {
        var work = Substitute.For<ISubmissionFlatteningProcessor>();
        ISubmissionFlatteningProcessor legacy = new LegacyProcessor(work);
        var ct = TestContext.Current.CancellationToken;

        (await legacy.TryBackfillAsync(new(11, 22, 33), true, ct)).Should().BeTrue();
        await work.Received(1).ProcessAsync(11, 22, 33, ct, includeIncomplete: true);
    }
}
