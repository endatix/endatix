using System.Reflection;
using System.Text;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

/// <summary>
/// Keeps the lifecycle event catalog, the webhook names, the settings keys and the docs in step. The docs tables
/// between the <c>lifecycle-*:start</c> and <c>:end</c> markers are rendered here; run with
/// <c>ENDATIX_UPDATE_DOCS=1</c> to rewrite them.
/// </summary>
public sealed class SubmissionLifecycleCatalogTests
{
    private const string UpdateDocsVariable = "ENDATIX_UPDATE_DOCS";

    [Fact]
    public void EventTypes_EveryKind_HasOneDistinctSubmissionType()
    {
        // Act
        var types = Enum.GetValues<SubmissionLifecycleEventKind>().Select(SubmissionLifecycleEvent.EventTypes.Of).ToList();

        // Assert
        types.Should().OnlyHaveUniqueItems().And.OnlyContain(type => type.StartsWith("submission.", StringComparison.Ordinal));
        SubmissionLifecycleEvent.EventTypes.All.Should().Equal(types);
    }

    [Fact]
    public void WebHookOperations_ForSubmissions_DeriveALifecycleTypeAndASettingsKey()
    {
        // Arrange
        var settingsKeys = typeof(WebHookSettings.WebHookEvents).GetProperties()
            .Where(property => property.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var submissionOperations = CurrentOperations().Where(operation => operation.Entity == nameof(Submission)).ToList();

        // Assert
        submissionOperations.Should().NotBeEmpty();
        foreach (var operation in submissionOperations)
        {
            var eventType = StringUtils.ToDottedEventType(operation.EventName);
            SubmissionLifecycleEvent.EventTypes.All.Should().Contain(eventType, operation.EventName);
            WebHookEvents.EventTypes.Should().Contain(eventType);
            settingsKeys.Should().Contain(StringUtils.ToPascalCase(operation.EventName));
        }
    }

    [Fact]
    public void WebHooksPluginEventNames_AreTheCurrentOperations()
    {
        // Act
        var eventNames = typeof(WebHooksPlugin.EventNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(field => (string)field.GetRawConstantValue()!);

        // Assert
        eventNames.Should().BeEquivalentTo(CurrentOperations().Select(operation => operation.EventName));
        WebHookEvents.EventTypes.Should().BeEquivalentTo(
            CurrentOperations().Select(operation => StringUtils.ToDottedEventType(operation.EventName)));
    }

    [Fact]
    public void LegacyEventTypes_AreNoLongerRaisedOrDelivered()
    {
        // Act
        var retired = LegacyOutboxEvents.All;

        // Assert
        retired.Should().NotIntersectWith(SubmissionLifecycleEvent.EventTypes.All);
        retired.Should().NotIntersectWith(WebHookEvents.EventTypes);
    }

    [Fact]
    public void Payloads_OnlyCompleted_CarriesTheAnswers()
    {
        // Arrange
        var completed = Submission.Create(Args(isComplete: true));
        var screenedOut = Submission.Create(Args(isComplete: false, outcome: CollectionStatus.ScreenOut));

        // Act
        var payloads = completed.DomainEvents.Concat(screenedOut.DomainEvents)
            .OfType<SubmissionLifecycleEvent>()
            .Select(lifecycle => (lifecycle.EventType, Json: JsonSerializer.SerializeToElement(
                lifecycle.GetPayload(), lifecycle.GetPayload().GetType(), JsonSerializerOptions.Web)))
            .ToList();

        // Assert
        payloads.Where(payload => payload.Json.TryGetProperty("jsonData", out _)).Select(payload => payload.EventType)
            .Should().Equal(SubmissionLifecycleEvent.EventTypes.Completed);
        payloads.Where(payload => !HasLifecycleFields(payload.Json)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("configuration/background-processing.mdx", "lifecycle-events")]
    [InlineData("configuration/background-processing.mdx", "lifecycle-phases")]
    [InlineData("guides/webhooks.mdx", "lifecycle-webhooks")]
    public void Docs_GeneratedBlock_EqualsCatalogRendering(string page, string block)
    {
        // Arrange
        var path = Path.Combine(DocsRoot(), page);
        var text = File.ReadAllText(path);
        var expected = Render(block);

        // Act
        var (actual, rewritten) = Replace(text, block, expected);
        if (Environment.GetEnvironmentVariable(UpdateDocsVariable) == "1" && actual != expected)
        {
            File.WriteAllText(path, rewritten);
            actual = expected;
        }

        // Assert
        actual.Should().Be(expected, $"{page} block {block} must match the catalog; run with {UpdateDocsVariable}=1 to rewrite it");
    }

    private static bool HasLifecycleFields(JsonElement payload) =>
        payload.TryGetProperty("tenantId", out _)
        && payload.TryGetProperty("isTestSubmission", out _)
        && payload.GetProperty("collectionStatus").TryGetProperty("phase", out _);

    // A blank line ends the table, so MDX reads the end marker as its own block.
    private static string Render(string block) => block switch
    {
        "lifecycle-events" => RenderEvents(),
        "lifecycle-phases" => RenderPhases(),
        "lifecycle-webhooks" => RenderWebHooks(),
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, null),
    } + "\n";

    private static string RenderEvents()
    {
        StringBuilder table = new();
        table.AppendLine("| Outbox `EventType` | Raised when | Webhook | Payload |");
        table.AppendLine("|--------------------|-------------|---------|---------|");
        foreach (var kind in Enum.GetValues<SubmissionLifecycleEventKind>())
        {
            var eventType = SubmissionLifecycleEvent.EventTypes.Of(kind);
            var webHook = WebHookEvents.TryGetOperation(eventType, out var operation)
                ? $"`{operation.EventName}` (setting `{StringUtils.ToPascalCase(operation.EventName)}`)"
                : "-";
            var payload = kind == SubmissionLifecycleEventKind.Completed
                ? "Lifecycle fields, plus `jsonData` and every field `submission_completed` has always carried"
                : kind == SubmissionLifecycleEventKind.Created
                    ? "Lifecycle fields, no `previousCollectionStatus`"
                    : "Lifecycle fields";
            table.AppendLine($"| `{eventType}` | {RaisedWhen(kind)} | {webHook} | {payload} |");
        }

        return table.ToString();
    }

    private static string RaisedWhen(SubmissionLifecycleEventKind kind)
    {
        if (kind == SubmissionLifecycleEventKind.Created)
        {
            return "A submission is created, from any entry point";
        }

        var phases = Enum.GetValues<CollectionPhase>();
        var moves = phases
            .SelectMany(from => phases.Select(to => (From: from, To: to)))
            .Where(move => SubmissionLifecycle.Decide(move.From, move.To) == kind)
            .ToList();
        if (kind == SubmissionLifecycleEventKind.CollectionStatusMoved)
        {
            return "The status moves to another code of the same phase: "
                + string.Join(", ", moves.Select(move => $"`{move.From.ToWireName()}`"));
        }

        var targets = moves.Select(move => move.To).Distinct().Select(phase => $"`{phase.ToWireName()}`");
        var sources = moves.Select(move => move.From).Distinct().Select(phase => $"`{phase.ToWireName()}`");
        return $"{string.Join(", ", sources)} → {string.Join(", ", targets)}";
    }

    private static string RenderPhases()
    {
        StringBuilder table = new();
        table.AppendLine("| Phase | Built-in codes | Can still move |");
        table.AppendLine("|-------|----------------|----------------|");
        var builtIn = typeof(CollectionStatusCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => CollectionStatus.FromCode((string)field.GetRawConstantValue()!))
            .ToList();
        foreach (var phase in Enum.GetValues<CollectionPhase>())
        {
            var codes = builtIn.Where(status => status.Phase == phase).Select(status => $"`{status.Code}`");
            var open = SubmissionLifecycle.Decide(phase, CollectionPhase.Closed) is not null ? "Yes" : "No, final";
            table.AppendLine($"| `{phase.ToWireName()}` | {string.Join(", ", codes)} | {open} |");
        }

        return table.ToString();
    }

    private static string RenderWebHooks()
    {
        StringBuilder table = new();
        table.AppendLine("| Webhook `eventName` | Settings key | Outbox event it delivers |");
        table.AppendLine("|---------------------|--------------|--------------------------|");
        foreach (var operation in CurrentOperations().Where(operation => operation.Entity == nameof(Submission)))
        {
            table.AppendLine(
                $"| `{operation.EventName}` | `{StringUtils.ToPascalCase(operation.EventName)}` | `{StringUtils.ToDottedEventType(operation.EventName)}` |");
        }

        return table.ToString();
    }

    private static (string Actual, string Rewritten) Replace(string text, string block, string expected)
    {
        var start = $"{{/* {block}:start - generated by SubmissionLifecycleCatalogTests */}}\n";
        var end = $"{{/* {block}:end */}}";
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        var endIndex = text.IndexOf(end, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < startIndex)
        {
            return ($"<missing markers {start.Trim()} … {end}>", text);
        }

        var contentStart = startIndex + start.Length;
        var actual = text[contentStart..endIndex];
        return (actual, text[..contentStart] + expected + text[endIndex..]);
    }

    private static IEnumerable<WebHookOperation> CurrentOperations() =>
        typeof(WebHookOperation).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(WebHookOperation) && field.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(field => (WebHookOperation)field.GetValue(null)!);

    private static string DocsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var docs = Path.Combine(directory.FullName, "docs", "endatix-docs", "docs");
            if (Directory.Exists(docs))
            {
                return docs;
            }
        }

        throw new DirectoryNotFoundException("docs/endatix-docs/docs not found above the test output.");
    }

    private static SubmissionCreateArgs Args(bool isComplete, CollectionStatus? outcome = null) =>
        new(TenantId: 1, FormId: 100, FormDefinitionId: 200, JsonData: """{"a":1}""", IsComplete: isComplete,
            StartSubmission: true, CollectionOutcome: outcome);
}
