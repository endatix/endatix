using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// App + Identity models: every <c>long Id</c> PK is OnAdd + client generator, never IDENTITY/serial.
/// Shared 1:1 keys (e.g. TenantSettings.TenantId) stay Never. Reporting/Jobs have their own suites.
/// </summary>
public class ApplySnowflakeIdValueGeneratorsTests
{
    [Fact]
    public void PostgreSql_App_LongKeys_AreClientSnowflakes_WithNoSerialIdentity()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        AssertLongKeyContract(context);
    }

    [Fact]
    public void SqlServer_App_LongKeys_AreClientSnowflakes_WithNoIdentity()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext();

        AssertLongKeyContract(context);
    }

    [Fact]
    public void PostgreSql_Identity_LongKeys_AreClientSnowflakes_WithNoSerialIdentity()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppIdentityDbContext();

        AssertLongKeyContract(context);
    }

    [Fact]
    public void SqlServer_Identity_LongKeys_AreClientSnowflakes_WithNoIdentity()
    {
        using var context = AppDbContextModelInspectionFactory.CreateSqlServerAppIdentityDbContext();

        AssertLongKeyContract(context);
    }

    [Fact]
    public void KeylessExportRow_HasNoSnowflakeGenerator()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        var exportRow = context.Model.FindEntityType(typeof(SubmissionExportRow));

        exportRow.Should().NotBeNull();
        exportRow!.FindPrimaryKey().Should().BeNull();
    }

    private static void AssertLongKeyContract(DbContext context)
    {
        var longKeyProperties = context.Model.GetEntityTypes()
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

            NpgsqlPropertyExtensions.GetValueGenerationStrategy(property).Should().Be(
                NpgsqlValueGenerationStrategy.None, "{0} must not be a Postgres serial/identity column", name);
            SqlServerPropertyExtensions.GetValueGenerationStrategy(property).Should().Be(
                SqlServerValueGenerationStrategy.None, "{0} must not be a SQL Server IDENTITY column", name);

            if (property.Name == "Id" && property.IsPrimaryKey())
            {
                property.ValueGenerated.Should().Be(ValueGenerated.OnAdd, "{0} must be generated on Add", name);
                property.GetValueGeneratorFactory().Should().NotBeNull("{0} must have a client value generator", name);
            }
        }
    }
}
