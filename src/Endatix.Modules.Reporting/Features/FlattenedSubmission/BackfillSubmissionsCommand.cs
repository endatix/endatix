using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

public sealed record BackfillSubmissionsCommand(
    long FormId,
    long TenantId,
    int BatchSize = 100,
    long? AfterSubmissionId = null,
    bool Force = false) : ICommand<Result<SubmissionBackfillResult>>;
