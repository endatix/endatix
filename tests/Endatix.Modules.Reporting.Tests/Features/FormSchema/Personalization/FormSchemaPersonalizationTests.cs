using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
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
        result.Columns.Select(column => column.Key).Should().Equal("city");
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_SchemaAlreadyHasTheColumns_ReturnsItWithoutLocking()
    {
        // Arrange
        var schema = Compiled(withSnapshot: true);

        // Act
        var result = await Personalization().IncludeAsync(schema, PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        result.Columns.Select(column => column.Key).Should().Equal("city", "AudienceIdentifier", "audience_city");
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, Arg.Any<CancellationToken>());
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
        result.Columns.Select(column => column.Key).Should().Equal("city", "AudienceIdentifier", "audience_city");
        locked.Codebook.Should().Contain("\"audience_city\"");
        locked.FormDefinitionRevision.Should().Be(FormDefinitionId);
        await _schemas.Received(1).SaveAsync(locked, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_AnotherFlattenAddedTheColumnsWhileThisOneWaited_SavesNothing()
    {
        // Arrange
        GivenLocked(Compiled(withSnapshot: true));
        GivenDefinition();

        // Act
        await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_DefinitionReadBeforeTheLock_IsNotReadAgainUnderIt()
    {
        // Arrange
        GivenLocked(Compiled());
        GivenDefinition();

        // Act
        await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        await _forms.Received(1).SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>());
        await _schemas.Received(1).SaveAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_RebuildMovedTheSchemaToANewerDefinitionWhileWaiting_CompilesFromTheNewerOne()
    {
        // Arrange: the flatten read definition 200; a rebuild to 201 committed before it took the lock.
        const long newerDefinitionId = 201;
        var compiled = new FormSchemaCompiler().CompilePersisted(Definition);
        FormSchemaEntity locked = new(TenantId, FormId, newerDefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson);
        GivenLocked(locked);
        GivenDefinition();

        // Act
        var result = await Personalization().IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        await _forms.Received(2).SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>());
        locked.FormDefinitionRevision.Should().Be(newerDefinitionId);
        result.Columns.Should().Contain(column => column.Key == "audience_city");
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
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_ColumnsWouldExceedTheColumnLimit_ReturnsTheSchemaWithoutThemAndDoesNotLock()
    {
        // Arrange: the form's one question fits; the two audience columns would not.
        FormSchemaCompiler compiler = new(new SchemaCompilationLimits { MaxColumns = 2 });

        // Act
        var result = await Personalization(compiler).IncludeAsync(Compiled(), PersonalizationReader.Read(Snapshot), TestContext.Current.CancellationToken);

        // Assert
        result.Columns.Select(column => column.Key).Should().Equal("city");
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, Arg.Any<CancellationToken>());
        await _forms.DidNotReceiveWithAnyArgs().SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_ColumnsAddedWhileWaitingReachTheColumnLimit_ReturnsTheLockedSchemaAndSavesNothing()
    {
        // Arrange: the schema this flatten read has room for its three columns; another flatten added audience_city
        // before the lock, and audience_age no longer fits.
        GivenLocked(Compiled(withSnapshot: true));
        GivenDefinition();
        FormSchemaCompiler compiler = new(new SchemaCompilationLimits { MaxColumns = 3 });

        // Act
        var result = await Personalization(compiler).IncludeAsync(
            Compiled(),
            PersonalizationReader.Read("""{"identifier":"CG-1","variables":{"age":65}}"""),
            TestContext.Current.CancellationToken);

        // Assert
        result.Columns.Select(column => column.Key).Should().Equal("city", "AudienceIdentifier", "audience_city");
        await _schemas.Received(1).LockAndGetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>());
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_ListedSubmitterFieldMissing_SavesItsColumnAtTheSchemasOwnRevision()
    {
        // Arrange
        var locked = Compiled();
        GivenLocked(locked);
        GivenDefinition();
        FormSchemaCompiler compiler = new(submitterProfileFields: new SubmitterProfileFields(["email"]));

        // Act
        var result = await Personalization(compiler).IncludeAsync(Compiled(), null, TestContext.Current.CancellationToken);

        // Assert
        result.Columns.Select(column => column.Key).Should().Equal("city", "submitter_email");
        locked.FormDefinitionRevision.Should().Be(FormDefinitionId);
        locked.Codebook.Should().Contain("\"submitter_profile\"");
        await _schemas.Received(1).SaveAsync(locked, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludeAsync_FormAtTheColumnLimitWithAListedSubmitterField_ReturnsTheSchemaWithoutItAndDoesNotLock()
    {
        // Arrange: the form's one question fills the column limit.
        FormSchemaCompiler compiler = new(
            new SchemaCompilationLimits { MaxColumns = 1 },
            new SubmitterProfileFields(["email"]));

        // Act
        var result = await Personalization(compiler).IncludeAsync(Compiled(), null, TestContext.Current.CancellationToken);

        // Assert
        result.Columns.Select(column => column.Key).Should().Equal("city");
        await _schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, Arg.Any<CancellationToken>());
        await _schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, Arg.Any<CancellationToken>());
    }

    private FormSchemaPersonalization Personalization(FormSchemaCompiler? compiler = null) =>
        new(_forms, _schemas, _unitOfWork, compiler ?? new FormSchemaCompiler(), NullLogger<FormSchemaPersonalization>.Instance);

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
