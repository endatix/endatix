using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

public sealed partial class ReportingExportRepositoryIntegrationTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(null, 0, false)]
    [InlineData(0, null, false)]
    public async Task Export_JoinedSubmission_UsesOnlyTheExactSourceOfTheFlattenedCells(int? seconds, int? revisions, bool matches)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(ct);
        await using var core = CreateAppDbContext();
        await using var reporting = CreateReportingDbContext();
        var id = seed.ProductionDay1Id;
        const string raw = """{"q":{"r":"skip","n":"skip"},"private":"not for export"}""";
        const string flat = """{"q__r":2,"q__n":null,"q__direct":80}""";
        await core.Submissions.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(v => v.JsonData, raw), ct);
        var source = await core.Submissions.AsNoTracking().SingleAsync(s => s.Id == id, ct);
        DateTime? stamp = seconds is { } t ? (source.ModifiedAt ?? source.CreatedAt).AddSeconds(t) : null;
        await reporting.FlattenedSubmissions.Where(r => r.SubmissionId == id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.DataJson, flat).SetProperty(r => r.SourceRevision, source.Revision + revisions)
            .SetProperty(r => r.SourceModifiedAt, stamp), ct);

        // Act
        var repo = CreateRepository(reporting, core);
        var rows = await CollectRowsAsync(repo, seed.FormId, new ExportQueryOptions(MinSubmissionId: id, MaxSubmissionId: id), ct);
        var row = rows.Single();
        var schema = new FormSchemaCompiler().CompilePersisted("""
            {"elements":[{"name":"q","type":"matrix","rows":["r","n","direct"],"columns":[80,{"value":2,"text":"Two"},"skip"]}]}
            """);
        using var projected = JsonDocument.Parse(CrunchTabularValueProjector.Project(row, schema.FlatteningMap, schema.CodebookJson));

        // Assert
        using var native = JsonDocument.Parse(row.DataJson);
        native.RootElement.GetProperty("q__r").GetInt32().Should().Be(2);
        projected.RootElement.GetProperty("q__r").GetRawText().Should().Be(matches ? "2" : "null");
        projected.RootElement.GetProperty("q__n").GetRawText().Should().Be(matches ? "2" : "null");
        projected.RootElement.GetProperty("q__direct").GetInt32().Should().Be(3);
        projected.RootElement.TryGetProperty("private", out _).Should().BeFalse();
    }
}
