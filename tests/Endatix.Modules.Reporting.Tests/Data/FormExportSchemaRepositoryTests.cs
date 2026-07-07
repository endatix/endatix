using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Data;

public class FormExportSchemaRepositoryTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionRevision = 200;
    private const string InitialSchemaJson = """{"columns":[]}""";
    private const string UpdatedSchemaJson = """{"columns":[{"key":"q1"}]}""";

    [Fact]
    public async Task GetByFormIdAsync_returns_null_when_schema_does_not_exist()
    {
        await using ReportingDbContext dbContext = ReportingDbContextTestFactory.Create(TenantId);
        FormExportSchemaRepository repository = ReportingDbContextTestFactory.CreateRepository(dbContext);

        FormExportSchema? result = await repository.GetByFormIdAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_persists_new_schema()
    {
        await using ReportingDbContext dbContext = ReportingDbContextTestFactory.Create(TenantId);
        FormExportSchemaRepository repository = ReportingDbContextTestFactory.CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);

        await repository.SaveAsync(schema, TestContext.Current.CancellationToken);

        FormExportSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision);
        persisted.SchemaJson.Should().Be(InitialSchemaJson);
        persisted.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_persists_updated_schema()
    {
        await using ReportingDbContext dbContext = ReportingDbContextTestFactory.Create(TenantId);
        FormExportSchemaRepository repository = ReportingDbContextTestFactory.CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);
        await repository.SaveAsync(schema, TestContext.Current.CancellationToken);

        schema.UpdateSchema(FormDefinitionRevision + 1, UpdatedSchemaJson);
        await repository.SaveAsync(schema, TestContext.Current.CancellationToken);

        FormExportSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision + 1);
        persisted.SchemaJson.Should().Be(UpdatedSchemaJson);
        dbContext.FormExportSchemas.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByFormIdAsync_is_scoped_to_tenant()
    {
        await using ReportingDbContext dbContext = ReportingDbContextTestFactory.Create(tenantId: 1);
        FormExportSchemaRepository repository = ReportingDbContextTestFactory.CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);
        await repository.SaveAsync(schema, TestContext.Current.CancellationToken);

        FormExportSchema? otherTenantResult = await repository.GetByFormIdAsync(
            tenantId: 2,
            FormId,
            TestContext.Current.CancellationToken);

        otherTenantResult.Should().BeNull();
    }
}
