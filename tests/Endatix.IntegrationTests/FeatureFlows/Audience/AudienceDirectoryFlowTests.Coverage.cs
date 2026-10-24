using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Variables;
using Endatix.Modules.Audience.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Audience coverage (endatix#1346): how many test and real participants have a value for each Property in the
/// variables read, and how many Properties each person lacks a value for on the people list.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Coverage_counts_participants_and_their_values_per_property_leaving_out_removed_and_deleted()
    {
        // Arrange
        var admin = await AdminAsync();
        var form = await FormWithCoverageAsync(admin);

        // Act
        var coverage = (await GetVariablesAsync(admin, form.FormId)).GetProperty("coverage");

        // Assert
        AssertJson(
            """
            {"participants":{"test":1,"real":2},
             "properties":[
               {"variableName":"city","filledTest":0,"filledReal":2},
               {"variableName":"seats","filledTest":1,"filledReal":1},
               {"variableName":"tier","filledTest":0,"filledReal":0}
             ]}
            """,
            coverage);
    }

    [Fact]
    public async Task The_people_list_counts_the_properties_each_person_has_no_value_for()
    {
        // Arrange
        var admin = await AdminAsync();
        var form = await FormWithCoverageAsync(admin);

        // Act
        var page = await ListPeopleAsync(admin, form.FormId, "");

        // Assert
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["ada@example.com"] = 1,
                ["bob@example.com"] = 2,
                ["tester@example.com"] = 2,
            },
            page.GetProperty("items").EnumerateArray().ToDictionary(
                person => person.GetProperty("identifier").GetString()!,
                person => person.GetProperty("notFilledIn").GetInt32()));
    }

    [Fact]
    public async Task Coverage_of_five_thousand_participants_by_fifty_properties_costs_the_statements_of_a_small_form()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var tenantId = world.Tenants[0].Id;
        var small = await FormWithCoverageAsync(admin);
        var largeFormId = await ImportedFormAsync(admin, new(People: 5_000, Properties: 50));
        await AddTestParticipantAsync(admin, largeFormId, "tester@example.com");

        // Act
        var smallRead = await CountedVariablesAsync(world.Services, tenantId, small.FormId);
        var largeRead = await CountedVariablesAsync(world.Services, tenantId, largeFormId);

        // Assert: p00 is left empty on every other row; the test participant has no values
        Assert.Equal(new ParticipantCounts(Test: 1, Real: 5_000), largeRead.Variables.Coverage.Participants);
        Assert.Equal(50, largeRead.Variables.Coverage.Properties.Count);
        Assert.Equal(new PropertyCoverage("p00", 0, 2_500), largeRead.Variables.Coverage.Properties[0]);
        Assert.All(
            largeRead.Variables.Coverage.Properties.Skip(1),
            property => Assert.Equal((0, 5_000), (property.FilledTest, property.FilledReal)));
        Assert.Equal(smallRead.Statements, largeRead.Statements);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"variables read at 5,000 x 50: {largeRead.Elapsed.TotalMilliseconds:F0} ms, {largeRead.Statements} statements");
    }

    [Fact]
    public async Task Coverage_of_five_thousand_test_participants_by_fifty_properties_counts_cells_in_one_statement()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var tenantId = world.Tenants[0].Id;
        var small = await FormWithCoverageAsync(admin);
        var largeFormId = await ImportedFormAsync(admin, new(People: 5_000, Properties: 50, IsTest: true));

        // Act
        var smallRead = await CountedVariablesAsync(world.Services, tenantId, small.FormId);
        var largeRead = await CountedVariablesAsync(world.Services, tenantId, largeFormId);

        // Assert: p00 is left empty on every other row
        var variables = largeRead.Variables;
        Assert.Equal(new ParticipantCounts(Test: 5_000, Real: 0), variables.Coverage.Participants);
        Assert.Equal(new PropertyCoverage("p00", 2_500, 0), variables.Coverage.Properties[0]);
        Assert.All(
            variables.Coverage.Properties.Skip(1),
            property => Assert.Equal((5_000, 0), (property.FilledTest, property.FilledReal)));
        Assert.Equal(5_000, variables.VariablePresets!.TotalPresets);
        Assert.Equal(AudienceVariablesReader.PresetLimit, variables.VariablePresets.Presets.Count);
        Assert.Equal(smallRead.Statements, largeRead.Statements);
        Assert.Single(largeRead.Commands, CountsCells);
        Assert.DoesNotContain(largeRead.Commands, FiltersTestMemberships);
        Assert.DoesNotContain(largeRead.Commands, JoinsCellsToMemberships);
        Assert.DoesNotContain(largeRead.Commands, ReadsCellsWithProperties);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"variables read at 5,000 test x 50: {largeRead.Elapsed.TotalMilliseconds:F0} ms, " +
            $"{largeRead.Statements} statements");
    }

    [Fact]
    public async Task A_value_saved_later_on_a_test_participant_counts_as_test()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var tester = await AddPersonWithValuesAsync(admin, formId, new("tester@example.com", IsTest: true, new()));

        // Act
        using var saved = await admin.PutValueAsync(
            $"/api/forms/{formId}/audience/people/{tester.MembershipId}", city, "Sofia");

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertJson(
            """
            {"participants":{"test":1,"real":0},
             "properties":[{"variableName":"city","filledTest":1,"filledReal":0}]}
            """,
            (await GetVariablesAsync(admin, formId)).GetProperty("coverage"));
    }

    [Fact]
    public async Task A_live_cell_of_a_deleted_property_stays_out_of_the_people_list_and_the_presets()
    {
        // Arrange: a cell left live on a Property deleted since, as a write racing the delete leaves it
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var gone = await admin.CreatePropertyAsync(formId, new { name = "Gone", dataType = "text" });
        var tester = await AddPersonWithValuesAsync(
            admin, formId, new("tester@example.com", IsTest: true, new() { [city] = "Sofia" }));
        using var deletion = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/properties/{gone}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        await StoreCellAsync(world.Services, new StoredCell(tester.MembershipId, gone, "x"));

        // Act
        var person = (await ListPeopleAsync(admin, formId, "")).GetProperty("items")[0];
        var preset = (await GetVariablesAsync(admin, formId))
            .GetProperty("variablePresets").GetProperty("presets")[0].GetProperty("variables");

        // Assert
        Assert.Equal([$"{city}"], person.GetProperty("values").EnumerateObject().Select(value => value.Name));
        Assert.Equal(0, person.GetProperty("notFilledIn").GetInt32());
        AssertJson("""{"city":"Sofia"}""", preset);
    }

    private sealed record CoverageForm(long FormId);

    /// <summary>
    /// City, Seats and Tier; ada (real: city, seats), bob (real: city), a tester (seats), a removed real participant
    /// with a city, and a deleted Property every one of them filled.
    /// </summary>
    private static async Task<CoverageForm> FormWithCoverageAsync(AudienceApi admin)
    {
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var seats = await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" });
        await admin.CreatePropertyAsync(formId, new { name = "Tier", dataType = "text" });
        var gone = await admin.CreatePropertyAsync(formId, new { name = "Gone", dataType = "text" });
        await AddPersonWithValuesAsync(
            admin, formId, new("ada@example.com", IsTest: false, new() { [city] = "Sofia", [seats] = "3", [gone] = "x" }));
        await AddPersonWithValuesAsync(
            admin, formId, new("bob@example.com", IsTest: false, new() { [city] = "Varna", [gone] = "x" }));
        await AddPersonWithValuesAsync(
            admin, formId, new("tester@example.com", IsTest: true, new() { [seats] = "1", [gone] = "x" }));
        var removed = await AddPersonWithValuesAsync(
            admin, formId, new("removed@example.com", IsTest: false, new() { [city] = "Ruse" }));
        using var removal = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{removed.MembershipId}", admin.CancellationToken);
        using var deletion = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/properties/{gone}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        return new CoverageForm(formId);
    }

    /// <summary>
    /// <see cref="ImportShape.People"/> participants, test ones when <see cref="ImportShape.IsTest"/>, imported with
    /// <see cref="ImportShape.Properties"/> text Properties p00…, every value filled except p00 on every other row.
    /// </summary>
    private sealed record ImportShape(int People, int Properties, bool IsTest = false);

    private static async Task<long> ImportedFormAsync(AudienceApi admin, ImportShape shape)
    {
        var formId = await admin.CreateFormAsync();
        var names = Enumerable.Range(0, shape.Properties).Select(index => $"p{index:D2}").ToList();
        foreach (var name in names)
        {
            await admin.CreatePropertyAsync(formId, new { name, dataType = "text" });
        }

        var csv = new StringBuilder("email,").AppendJoin(',', names).Append('\n');
        for (var row = 0; row < shape.People; row++)
        {
            csv.Append("person").Append(row).Append("@example.com,").Append(row % 2 == 0 ? "" : "v")
                .Append(',').AppendJoin(',', names.Skip(1).Select(_ => "v")).Append('\n');
        }

        using var imported = await admin.PostAsync(
            $"/api/forms/{formId}/audience/import",
            new
            {
                csvText = csv.ToString(),
                identifierColumn = "email",
                fileName = "people.csv",
                propertyColumns = names.ToDictionary(name => name),
                isTest = shape.IsTest,
            });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(shape.People, (await admin.ReadAsync(imported)).GetProperty("createdCount").GetInt32());
        return formId;
    }

    /// <summary>A statement that counts cells: the coverage count, one GROUP BY for test and real together.</summary>
    private static bool CountsCells(string sql) =>
        sql.Contains("PropertyValues", StringComparison.Ordinal) && sql.Contains("GROUP BY", StringComparison.Ordinal);

    /// <summary>
    /// A statement that filters memberships by the test flag (<c>AND m."IsTest"</c>, <c>AND [m].[IsTest] = …</c>), as
    /// the Test ids the coverage count once sent back were read; the participant counts group by it instead.
    /// </summary>
    private static bool FiltersTestMemberships(string sql) =>
        sql.Contains("Memberships", StringComparison.Ordinal) && TestFilter().IsMatch(sql);

    /// <summary>
    /// A statement that reads cells with the Properties table: the <c>EXISTS</c> on live Properties that let both
    /// providers scan the tenant's cells for 20 participants (#1376). Cells are read for the Property ids already read.
    /// </summary>
    private static bool ReadsCellsWithProperties(string sql) =>
        sql.Contains("PropertyValues", StringComparison.Ordinal)
        && (sql.Contains("\"Properties\"", StringComparison.Ordinal)
            || sql.Contains("[Properties]", StringComparison.Ordinal));

    /// <summary>A statement that reads cells and memberships together: the join or subquery #1363 removed.</summary>
    private static bool JoinsCellsToMemberships(string sql) =>
        sql.Contains("PropertyValues", StringComparison.Ordinal)
        && sql.Contains("Memberships", StringComparison.Ordinal);

    [GeneratedRegex(@"AND \S+IsTest\W")]
    private static partial Regex TestFilter();

    private sealed record CountedVariables(
        AudienceVariablesDto Variables,
        int Statements,
        IReadOnlyList<string> Commands,
        TimeSpan Elapsed);

    /// <summary>The variables read in its own scope, counting the statements its contexts send and timing it.</summary>
    private static async Task<CountedVariables> CountedVariablesAsync(
        IServiceProvider services,
        long tenantId,
        long formId)
    {
        await using var scope = services.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            (DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(),
        ];
        using var statements = new StatementCounter(contexts);
        var stopwatch = Stopwatch.StartNew();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new GetAudienceVariablesQuery(tenantId, formId),
            TestContext.Current.CancellationToken);
        stopwatch.Stop();
        Assert.True(result.IsSuccess);
        return new CountedVariables(result.Value, statements.Count, statements.Commands, stopwatch.Elapsed);
    }
}
