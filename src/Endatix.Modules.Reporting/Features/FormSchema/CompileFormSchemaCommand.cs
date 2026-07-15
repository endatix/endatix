using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Command to compile the form schema.
/// </summary>
public sealed record CompileFormSchemaCommand(
    long FormId,
    long TenantId) : ICommand<Result<CompileFormSchemaResult>>;

/// <summary>
/// Result of the compile form schema command.
/// </summary>
public sealed record CompileFormSchemaResult(
    long FormId,
    long FormDefinitionId);
