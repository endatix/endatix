using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// App + Identity models: every <c>long Id</c> PK is OnAdd + client generator, never IDENTITY/serial,
/// and carries only the active provider's value-generation annotation.
/// Shared 1:1 keys (e.g. TenantSettings.TenantId) stay Never. Reporting/Jobs have their own suites.
/// </summary>
public class ApplySnowflakeIdValueGeneratorsTests
{
    private const string NpgsqlStrategyAnnotation = "Npgsql:ValueGenerationStrategy";
    private const string SqlServerStrategyAnnotation = "SqlServer:ValueGenerationStrategy";

    [Fact]
    public void PostgreSql_App_LongKeys_AreClientSnowflakes_WithNoSerialIdentity()
    {
        // Arrange
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        // Act
        var model = DesignTimeModelOf(context);

        // Assert
        AssertLongKeyContract(model, IsNotNpgsqlStoreGenerated, SqlServerStrategyAnnotation);
    }

    [Fact]
    public void SqlServer_App_LongKeys_AreClientSnowflakes_WithNoIdentity()
    {
        // Arrange
        using AppDbContext context = AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext();

        // Act
        var model = DesignTimeModelOf(context);

        // Assert
        AssertLongKeyContract(model, IsNotSqlServerStoreGenerated, NpgsqlStrategyAnnotation);
    }

    [Fact]
    public void PostgreSql_Identity_LongKeys_AreClientSnowflakes_WithNoSerialIdentity()
    {
        // Arrange
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppIdentityDbContext();

        // Act
        var model = DesignTimeModelOf(context);

        // Assert
        AssertLongKeyContract(model, IsNotNpgsqlStoreGenerated, SqlServerStrategyAnnotation);
    }

    [Fact]
    public void SqlServer_Identity_LongKeys_AreClientSnowflakes_WithNoIdentity()
    {
        // Arrange
        using var context = AppDbContextModelInspectionFactory.CreateSqlServerAppIdentityDbContext();

        // Act
        var model = DesignTimeModelOf(context);

        // Assert
        AssertLongKeyContract(model, IsNotSqlServerStoreGenerated, NpgsqlStrategyAnnotation);
    }

    [Fact]
    public void KeylessExportRow_HasNoSnowflakeGenerator()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        var exportRow = context.Model.FindEntityType(typeof(SubmissionExportRow));

        exportRow.Should().NotBeNull();
        exportRow!.FindPrimaryKey().Should().BeNull();
    }

    [Fact]
    public void Add_StampsDistinctSnowflakeIds_BeforeSaveChanges()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(1L);
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext(tenantContext);

        Form added = Form.Create(new FormCreateArgs(TenantId: 1, Name: "A"));
        Form rangeFirst = Form.Create(new FormCreateArgs(TenantId: 1, Name: "B"));
        Form rangeSecond = Form.Create(new FormCreateArgs(TenantId: 1, Name: "C"));
        added.Id.Should().Be(0);
        rangeFirst.Id.Should().Be(0);
        rangeSecond.Id.Should().Be(0);

        context.Forms.Add(added);
        context.Forms.AddRange(rangeFirst, rangeSecond);

        added.Id.Should().BeGreaterThan(0);
        rangeFirst.Id.Should().BeGreaterThan(0);
        rangeSecond.Id.Should().BeGreaterThan(0);
        new[] { added.Id, rangeFirst.Id, rangeSecond.Id }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Add_Tenant_StampsSnowflakeId_BeforeSaveChanges()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(1L);
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext(tenantContext);

        Tenant tenant = new("Acme", "abcd1234");
        tenant.Id.Should().Be(0);

        context.Set<Tenant>().Add(tenant);

        tenant.Id.Should().BeGreaterThan(0);
    }

    private static IModel DesignTimeModelOf(DbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static bool IsNotNpgsqlStoreGenerated(IReadOnlyProperty property) =>
        NpgsqlPropertyExtensions.GetValueGenerationStrategy(property) == NpgsqlValueGenerationStrategy.None;

    private static bool IsNotSqlServerStoreGenerated(IReadOnlyProperty property) =>
        SqlServerPropertyExtensions.GetValueGenerationStrategy(property) == SqlServerValueGenerationStrategy.None;

    private static void AssertLongKeyContract(
        IModel model,
        Func<IReadOnlyProperty, bool> isNotStoreGenerated,
        string otherProviderStrategyAnnotation)
    {
        var longKeyProperties = model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .SelectMany(entityType => entityType.GetKeys())
            .SelectMany(key => key.Properties)
            .Where(property => property.ClrType == typeof(long))
            .Distinct()
            .ToList();

        longKeyProperties.Should().NotBeEmpty("the model should map entities with long keys");

        using var scope = new AssertionScope();
        foreach (var property in longKeyProperties)
        {
            var name = $"{property.DeclaringType.ShortName()}.{property.Name}";

            isNotStoreGenerated(property).Should().BeTrue("{0} must not be a serial/IDENTITY column", name);
            property.FindAnnotation(otherProviderStrategyAnnotation).Should().BeNull(
                "{0} must carry only the active provider's strategy so its snapshot compiles without the other provider", name);

            if (property.Name == "Id" && property.IsPrimaryKey())
            {
                property.ValueGenerated.Should().Be(ValueGenerated.OnAdd, "{0} must be generated on Add", name);
                property.GetValueGeneratorFactory().Should().NotBeNull("{0} must have a client value generator", name);
            }
        }
    }
}
