using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
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
        var largeFormId = await ImportedFormAsync(admin, people: 5_000, properties: 50);
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
    /// <paramref name="people"/> real participants imported with <paramref name="properties"/> text Properties p00…,
    /// every value filled except p00 on every other row.
    /// </summary>
    private static async Task<long> ImportedFormAsync(AudienceApi admin, int people, int properties)
    {
        var formId = await admin.CreateFormAsync();
        var names = Enumerable.Range(0, properties).Select(index => $"p{index:D2}").ToList();
        foreach (var name in names)
        {
            await admin.CreatePropertyAsync(formId, new { name, dataType = "text" });
        }

        var csv = new StringBuilder("email,").AppendJoin(',', names).Append('\n');
        for (var row = 0; row < people; row++)
        {
            csv.Append("person").Append(row).Append("@example.com,").Append(row % 2 == 0 ? "" : "v")
                .Append(',').AppendJoin(',', names.Skip(1).Select(_ => "v")).Append('\n');
        }

        using var imported = await admin.ImportAsync(formId, csv.ToString(), names.ToDictionary(name => name));
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(people, (await admin.ReadAsync(imported)).GetProperty("createdCount").GetInt32());
        return formId;
    }

    private sealed record CountedVariables(AudienceVariablesDto Variables, int Statements, TimeSpan Elapsed);

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
        return new CountedVariables(result.Value, statements.Count, stopwatch.Elapsed);
    }
}
