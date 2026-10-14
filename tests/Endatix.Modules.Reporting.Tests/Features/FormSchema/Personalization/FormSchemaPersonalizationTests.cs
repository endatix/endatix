using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

public sealed class FormSchemaPersonalizationTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const string Definition = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"city"}]}]}""";
    private const string Snapshot = """{"identifier":"CG-1","variables":{"city":"Columbus"}}""";

    private readonly IFormsRepository _forms = Substitute.For<IFormsRepository>();
    private readonly IFormSchemaRepository _schemas = Substitute.For<IFormSchemaRepository>();
    private readonly IReportingUnitOfWork _unitOfWork = Substitute.For<IReportingUnitOfWork>();

    [Fact]
    public async Task IncludeAsync_ResponseWithoutSnapshot_ReturnsTheSchemaWithoutLocking()
    {
        // Arrange
        var schema = Compiled();

        // Act
        var result = await Personalization().IncludeAsync(schema, null, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(schema);
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, default);
    }

    [Fact]
    public async Task IncludeAsync_SchemaAlreadyHasTheColumns_ReturnsItWithoutLocking()
    {
        // Arrange
        var schema = Compiled(withSnapshot: true);

        // Act
        var result = await Personalization().IncludeAsync(schema, PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(schema);
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, default);
    }

    [Fact]
    public async Task IncludeAsync_NewVariables_SavesTheSchemaWithTheirColumnsUnderTheLock()
    {
        // Arrange
        var locked = Compiled();
        GivenLocked(locked);
        GivenDefinition();

        // Act
        var result = await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(locked);
        FormSchemaFlatteningMap.FromJson(result.FlatteningMap).Columns.Select(column => column.Key)
            .Should().Equal("city", "AudienceIdentifier", "audience_city");
        result.Codebook.Should().Contain("\"audience_city\"");
        result.FormDefinitionRevision.Should().Be(FormDefinitionId);
        await _schemas.Received(1).SaveAsync(locked, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_AnotherFlattenAddedTheColumnsWhileThisOneWaited_SavesNothing()
    {
        // Arrange
        GivenLocked(Compiled(withSnapshot: true));

        // Act
        await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _forms.DidNotReceiveWithAnyArgs().SingleOrDefaultAsync(default(DefinitionByFormAndDefinitionIdSpec)!, default);
    }

    [Fact]
    public async Task IncludeAsync_DefinitionGone_SavesNothing()
    {
        // Arrange
        GivenLocked(Compiled());
        _forms.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((FormDefinition?)null);

        // Act
        await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    private FormSchemaPersonalization Personalization() =>
        new(_forms, _schemas, _unitOfWork, new FormSchemaCompiler(), NullLogger<FormSchemaPersonalization>.Instance);

    private static FormSchemaEntity Compiled(bool withSnapshot = false)
    {
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(Definition);
        if (withSnapshot)
        {
            compiled = compiler.IncludePersonalization(
                Definition,
                new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
                PersonalizationColumns.From(PersonalizationReader.Read(Snapshot)));
        }

        return new FormSchemaEntity(TenantId, FormId, FormDefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson);
    }

    private void GivenLocked(FormSchemaEntity schema) =>
        _schemas.LockAndGetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(schema);

    private void GivenDefinition() =>
        _forms.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new FormDefinition(tenantId: TenantId, jsonData: Definition) { Id = FormDefinitionId });
}
