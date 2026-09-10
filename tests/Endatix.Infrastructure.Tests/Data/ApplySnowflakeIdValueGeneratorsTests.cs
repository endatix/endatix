using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Endatix.Infrastructure.Tests.Data;

public class ApplySnowflakeIdValueGeneratorsTests
{
    [Fact]
    public void FormId_IsClientGeneratedOnAdd_WithoutNpgsqlIdentity()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();
        IProperty id = context.Model.FindEntityType(typeof(Form))!.FindProperty(nameof(Form.Id))!;

        id.ValueGenerated.Should().Be(ValueGenerated.OnAdd);
        id.FindAnnotation("Npgsql:ValueGenerationStrategy")!.Value
            .Should().Be(NpgsqlValueGenerationStrategy.None);
        id.GetValueGeneratorFactory().Should().NotBeNull();
    }

    [Fact]
    public void KeylessExportRow_HasNoSnowflakeGenerator()
    {
        using AppDbContext context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();
        var exportRow = context.Model.FindEntityType(typeof(SubmissionExportRow));
        exportRow.Should().NotBeNull();
        exportRow!.FindPrimaryKey().Should().BeNull();
    }
}
