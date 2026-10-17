using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Infrastructure.Data.Querying;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;

namespace Endatix.IntegrationTests;

/// <summary>Form definitions and answers for the search copy tests, and the key filter of the provider under test.</summary>
internal static class SearchCopyForms
{
    /// <summary>A text, a comment, a dropdown with numeric values, a date and a date-time question.</summary>
    public const string Mixed = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "company" },
          { "type": "comment", "name": "notes" },
          { "type": "dropdown", "name": "size", "choices": [1, 2, 3] },
          { "type": "text", "name": "visit", "inputType": "date" },
          { "type": "text", "name": "arrival", "inputType": "datetime-local" },
          { "type": "text", "name": "reference", "readOnly": true }
        ] } ] }
        """;

    /// <summary><paramref name="count"/> text questions named <c>q001</c>, <c>q002</c>, … in that order.</summary>
    public static string TextQuestions(int count)
    {
        JsonArray elements = [];
        foreach (var name in QuestionNames(count))
        {
            elements.Add(new JsonObject { ["type"] = "text", ["name"] = name });
        }

        return new JsonObject { ["pages"] = new JsonArray(new JsonObject { ["elements"] = elements }) }.ToJsonString();
    }

    public static IEnumerable<string> QuestionNames(int count) =>
        Enumerable.Range(1, count).Select(index => $"q{index:D3}");

    public static string Answers(object answers) => JsonSerializer.Serialize(answers);

    public static IReadOnlyList<string> SearchKeyNames(FormSchema schema) =>
        FormSearchKeys.FromJson(schema.SearchKeys).Keys.Select(key => key.Key).ToList();

    public static IRelationalJsonObjectKeyFilter KeyFilter(TestDatabaseProvider provider) =>
        provider == TestDatabaseProvider.SqlServer ? new SqlServerJsonObjectKeyFilter() : new NpgsqlJsonObjectKeyFilter();
}
