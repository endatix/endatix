using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Command to compile the form schema.
/// </summary>
/// <param name="FormId">The form to compile.</param>
/// <param name="TenantId">The tenant that owns the form.</param>
/// <param name="Replace">
/// When <c>true</c>, always replace the schema and clear flattened rows.
/// When <c>false</c> (default), auto-pick Replace vs Merge from real submission count.
/// </param>
public sealed record CompileFormSchemaCommand(
    long FormId,
    long TenantId,
    bool Replace = false) : ICommand<Result<CompileFormSchemaResult>>;

/// <summary>
/// Result of the compile form schema command.
/// </summary>
public sealed record CompileFormSchemaResult(
    long FormId,
    long FormDefinitionId);
