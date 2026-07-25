using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

/// <summary>
/// Early-exit processor cases. Replace vs merge gate + persist behavior are covered by
/// FormSchemaCompilerTests and FormSchemaProcessorReplaceMergeIntegrationTests (real AppDbContext).
/// </summary>
public class FormSchemaProcessorTests
{
  private const long TenantId = 1;
  private const long FormId = 100;
  private const long FormDefinitionId = 200;

  [Fact]
  public async Task FormSchemaProcessor_ProcessAsync_WithMissingDefinition_DoesNotPersistSchema()
  {
    IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
    IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
    IFlattenedSubmissionRepository flattenedRepository = Substitute.For<IFlattenedSubmissionRepository>();
    formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
        .Returns((FormDefinition?)null);

    FormSchemaProcessor processor = CreateProcessor(
        formsRepository,
        schemaRepository,
        flattenedRepository);

    await processor.ProcessAsync(TenantId, FormId, formDefinitionId: 999, TestContext.Current.CancellationToken);

    await schemaRepository.DidNotReceive().GetByFormIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<CancellationToken>());
    await flattenedRepository.DidNotReceive()
        .DeleteByFormIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task FormSchemaProcessor_ProcessAsync_WithTenantMismatch_ThrowsInvalidOperationException()
  {
    IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
    IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
    IFlattenedSubmissionRepository flattenedRepository = Substitute.For<IFlattenedSubmissionRepository>();
    FormDefinition definition = new(tenantId: 2, jsonData: """{"pages":[]}""") { Id = FormDefinitionId };
    formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
        .Returns(definition);

    FormSchemaProcessor processor = CreateProcessor(
        formsRepository,
        schemaRepository,
        flattenedRepository);

    Func<Task> act = () => processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*Tenant mismatch*");
    await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<CancellationToken>());
  }

  private static FormSchemaProcessor CreateProcessor(
      IFormsRepository formsRepository,
      IFormSchemaRepository schemaRepository,
      IFlattenedSubmissionRepository flattenedRepository) =>
      new(
          formsRepository,
          schemaRepository,
          flattenedRepository,
          Substitute.For<IReportingUnitOfWork>(),
          // Early-exit tests never query submissions.
          Substitute.For<AppDbContext>(),
          new FormSchemaCompiler(),
          NullLogger<FormSchemaProcessor>.Instance);
}
