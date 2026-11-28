using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Versioning;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed class NumberFormDefinitionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upgrade_preserves_the_live_id_and_adopts_the_last_stray_before_cleanup()
    {
        await using var host = await OwnDatabaseWebHost.StartAsync();
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Ct);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = await CreateFormAsync(admin);
        var active = (await db.Forms.SingleAsync(row => row.Id == form, Ct)).ActiveDefinitionId!.Value;
        var all = db.Database.GetMigrations().ToList();
        var firstDraft = all.FindIndex(id => id.EndsWith("_AddFormDraftHistory", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(all[firstDraft - 1], Ct);
        await SeedLegacyAsync(db, active);

        var previousRevision = await db.Forms.Where(row => row.Id == form).Select(row => row.Revision).SingleAsync(Ct);
        await db.Database.MigrateAsync(Ct);
        db.ChangeTracker.Clear();
        await AssertAdoptionAsync(db, form, active);
        await AssertCompileEventAsync(db, form, previousRevision + 1);
        await InsertOldShapeAsync(db, active, 104);
        await db.Database.MigrateAsync(Ct);
        await AssertLegacyWriteAsync(admin, form, active);
        var definitions = await db.FormDefinitions.AsNoTracking().Where(row => row.FormId == form).ToListAsync(Ct);
        definitions.Single(row => row.Id == active).Number.Should().Be(1);
        definitions.Single(row => row.Id == active).ContentHash.Should().Be(CanonicalSurveyJson.PublishedHash("""{"title":"Edited"}"""));
        definitions.Single(row => row.Id == 104).Number.Should().BeNull();
        definitions.Single(row => row.Id == 104).IsDraft.Should().BeTrue();
    }

    private static async Task<long> CreateFormAsync(HttpClient admin)
    {
        using var response = await admin.PostAsJsonAsync("/api/forms", new { name = "Upgrade", isEnabled = true, formDefinitionJsonData = "{}" }, Ct);
        response.EnsureSuccessStatusCode();
        return long.Parse((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!);
    }

    private static async Task SeedLegacyAsync(AppDbContext db, long active)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "FormDefinitions" SET "IsDraft" = {true}, "CreatedAt" = {DateTime.UnixEpoch}, "ModifiedAt" = NULL
            WHERE "Id" = {active}
            """, Ct);
        await InsertOldShapeAsync(db, active, 101);
        await InsertOldShapeAsync(db, active, 102);
        await InsertOldShapeAsync(db, active, 103);
        var seed = await db.Forms.SingleAsync(row => row.ActiveDefinitionId == active, Ct);
        db.Submissions.Add(Submission.Create(new SubmissionCreateArgs(seed.TenantId, seed.Id, 101, "{}")));
        await db.SaveChangesAsync(Ct);
    }

    private static Task InsertOldShapeAsync(AppDbContext db, long active, long id) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO "FormDefinitions" ("Id","FormId","TenantId","IsDraft","IsDeleted","CreatedAt","JsonData")
            SELECT {{id}}, "FormId", "TenantId", {{true}}, {{false}}, {{DateTime.UnixEpoch.AddDays(1)}},
                CAST(CASE WHEN {{id}} = 103 THEN '{"z":2,"a":1}' ELSE '{}' END AS json)
            FROM "FormDefinitions" WHERE "Id" = {{active}}
            """, Ct);

    private static async Task AssertAdoptionAsync(AppDbContext db, long form, long active)
    {
        var definitions = await db.FormDefinitions.IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(row => row.FormId == form).ToDictionaryAsync(row => row.Id, Ct);
        definitions[active].Number.Should().Be(1);
        definitions[active].IsDraft.Should().BeFalse();
        definitions[101].Number.Should().Be(2);
        definitions[101].IsDraft.Should().BeFalse();
        definitions[102].IsDeleted.Should().BeTrue();
        definitions[103].IsDeleted.Should().BeTrue();
        (await db.Forms.SingleAsync(row => row.Id == form, Ct)).ActiveDefinitionId.Should().Be(active);
        var head = await db.FormDrafts.SingleAsync(row => row.FormId == form, Ct);
        const string expectedHash = "99168216144c7fed5d4c54916cf98d9c66096280c04a499822a99b6658bd177a";
        head.ContentHash.Should().Be(expectedHash);
        var tape = await db.FormDraftOperations.Where(row => row.FormId == form).OrderBy(row => row.Version).ToListAsync(Ct);
        tape.Select(row => row.Reason).Should().Equal("seed", "adopt");
        tape[1].Actor.Kind.Should().Be("system");
        tape[1].ContentHash.Should().Be(expectedHash);
        tape[1].Payload.Should().Be(head.JsonData);
    }

    private static async Task AssertCompileEventAsync(AppDbContext db, long form, long revision)
    {
        var owner = await db.Forms.SingleAsync(row => row.Id == form, Ct);
        owner.Revision.Should().Be(revision);
        var payloads = await db.OutboxMessages.Where(row => row.EventType == "form.definition.updated")
            .Select(row => row.Payload).ToListAsync(Ct);
        var events = payloads.Select(payload => JsonSerializer.Deserialize<JsonElement>(payload));
        events.Should().ContainSingle(payload => payload.GetProperty("formId").GetString() == form.ToString()
            && payload.GetProperty("revision").GetString() == revision.ToString()
            && payload.GetProperty("formDefinitionId").GetString() == owner.ActiveDefinitionId.ToString());
    }

    private static async Task AssertLegacyWriteAsync(HttpClient admin, long form, long active)
    {
        var route = $"/api/forms/{form}/definitions/{active}";
        using var updated = await admin.PutAsJsonAsync(route, new { isDraft = false, jsonData = """{"title":"Edited"}""" }, Ct);
        updated.EnsureSuccessStatusCode();
        var read = await admin.GetFromJsonAsync<JsonElement>(route, Ct);
        read.GetProperty("id").GetString().Should().Be(active.ToString());
        read.GetProperty("jsonData").GetString().Should().Contain("Edited");
    }

    [Fact]
    public async Task Publication_survives_reload_and_cannot_be_edited_by_erasing_provenance()
    {
        await using var host = await OwnDatabaseWebHost.StartAsync();
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Ct);
        var id = await CreateFormAsync(admin);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = await db.Forms.SingleAsync(row => row.Id == id, Ct);
        const string json = """{"n":1e+02}""";
        var published = FormDefinition.FromDraft(form, new(2, 0, json), new(DateTime.UtcNow, null));
        form.Publish(published, "Ready");
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        var stored = await db.FormDefinitions.SingleAsync(row => row.Id == published.Id, Ct);
        stored.JsonData.Should().Be(json);
        stored.ContentHash.Should().Be(CanonicalSurveyJson.Hash(json));
        var entry = db.Entry(stored);
        entry.Property(row => row.PublishedFromDraftVersion).CurrentValue = null;
        entry.Property(row => row.JsonData).CurrentValue = "{}";
        var save = () => db.SaveChangesAsync(Ct);
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*insert-only*");
        db.ChangeTracker.Clear();
        (await db.FormDefinitions.SingleAsync(row => row.Id == published.Id, Ct)).PublishedFromDraftVersion.Should().Be(0);

        using var deleted = await admin.DeleteAsync($"/api/forms/{id}", Ct);
        deleted.EnsureSuccessStatusCode();
        (await db.FormDefinitions.AnyAsync(row => row.Id == published.Id, Ct)).Should().BeFalse();
        (await db.FormDefinitions.IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .AsNoTracking().SingleAsync(row => row.Id == published.Id, Ct)).IsDeleted.Should().BeFalse();
    }
}
