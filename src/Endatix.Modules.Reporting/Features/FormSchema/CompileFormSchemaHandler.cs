using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Handler for the compile form schema command.
/// </summary>
public sealed class CompileFormSchemaHandler(
    IRepository<Form> formsRepository,
    IFormSchemaProcessor schemaProcessor) : ICommandHandler<CompileFormSchemaCommand, Result<CompileFormSchemaResult>>
{
    /// <inheritdoc/>
    public async Task<Result<CompileFormSchemaResult>> Handle(
        CompileFormSchemaCommand request,
        CancellationToken cancellationToken)
    {
        ActiveFormDefinitionByFormIdSpec spec = new(request.FormId);
        var form = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        if (form.ActiveDefinition is null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        var formDefinitionId = form.ActiveDefinition.Id;

        await schemaProcessor.ProcessAsync(
            request.TenantId,
            request.FormId,
            formDefinitionId,
            cancellationToken);

        return Result.Success(
            new CompileFormSchemaResult(request.FormId, formDefinitionId));
    }
}
