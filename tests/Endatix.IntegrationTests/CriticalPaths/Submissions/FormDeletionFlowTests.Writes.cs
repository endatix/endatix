using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The writes the deletion tests send to a submission, as theory rows. In a route, <c>{id}</c> is the submission id,
/// <c>{token}</c> its submission token and <c>{accessToken}</c> an edit access token minted for it.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    private const string ChangedAnswers = """{"jsonData":"{\"q\":\"changed\"}"}""";

    /// <summary>The write's first save of a submission: by token, that is the token's extend.</summary>
    private const string FirstSubmissionSave = """UPDATE ["\[]Submissions["\]]""";

    /// <summary>The save of the submission's answers, after a by-token write extended its token.</summary>
    private const string AnswersSave = """UPDATE ["\[]Submissions["\]] SET .*JsonData""";

    /// <summary>
    /// Every route that writes a live submission: management, its test mark, by submission token, by access token, and
    /// its own delete, which a submission of a deleted form is not found by either.
    /// </summary>
    private static readonly Dictionary<string, SubmissionWrite> ChangingWrites = new()
    {
        ["PATCH answers"] = new("PATCH", "{id}", ChangedAnswers),
        ["PATCH complete"] = new("PATCH", "{id}", """{"isComplete":true,"jsonData":"{\"q\":\"done\"}"}"""),
        ["PUT"] = new("PUT", "{id}", """{"isComplete":false,"jsonData":"{\"q\":\"replaced\"}","currentPage":1}"""),
        ["POST status"] = new("POST", "{id}/status", $$"""{"status":"{{SubmissionStatusCodes.Read}}"}"""),
        ["PATCH test"] = new("PATCH", "{id}/test", """{"isTest":true}"""),
        ["PATCH by token"] = new("PATCH", "by-token/{token}", ChangedAnswers),
        ["PATCH by access token"] = new("PATCH", "by-access-token/{accessToken}", ChangedAnswers),
        ["DELETE"] = new("DELETE", "{id}", null),
    };

    /// <summary>
    /// The writes, each paused at its first save of the submission, and the by-token PATCH once more, paused at its
    /// answers' save.
    /// </summary>
    private static readonly Dictionary<string, SubmissionWrite> RacingWrites =
        new(ChangingWrites)
        {
            ["PATCH by token, at the answers' save"] = ChangingWrites["PATCH by token"] with { PauseAt = AnswersSave },
        };

    public static TheoryData<string> SubmissionWrites => new(ChangingWrites.Keys);

    public static TheoryData<string> RacingSubmissionWrites => new(RacingWrites.Keys);

    /// <param name="PauseAt">The statement a race pauses the write at.</param>
    private sealed record SubmissionWrite(string Method, string Route, string? Body, string PauseAt = FirstSubmissionSave);

    private sealed record WriteTarget(long FormId, long SubmissionId, string Token, string AccessToken);

    /// <summary>What a write would change, compared before and after; the deletion's own stamps are left out.</summary>
    private sealed record SubmissionState(string JsonData, bool IsComplete, string Status, string? Token, DateTime? TokenExpiresAt);

    /// <summary>A new submission of the form and both its tokens, read while the form is live.</summary>
    private static async Task<WriteTarget> WriteTargetAsync(IServiceProvider services, HttpClient admin, long formId)
    {
        var submissionId = await CreateSubmissionAsync(admin, formId);
        var state = await StateAsync(services, submissionId);
        using var minted = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions = new[] { "view", "edit" } },
            Ct);
        Assert.Equal(HttpStatusCode.OK, minted.StatusCode);
        var accessToken = (await minted.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetString()!;
        return new WriteTarget(formId, submissionId, state.Token!, accessToken);
    }

    private static HttpRequestMessage WriteRequest(SubmissionWrite write, WriteTarget target)
    {
        var route = write.Route
            .Replace("{id}", target.SubmissionId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{token}", target.Token, StringComparison.Ordinal)
            .Replace("{accessToken}", target.AccessToken, StringComparison.Ordinal);
        var request = new HttpRequestMessage(new HttpMethod(write.Method), $"/api/forms/{target.FormId}/submissions/{route}");
        if (write.Body is not null)
        {
            request.Content = new StringContent(write.Body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static async Task<SubmissionState> StateAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await AllSubmissions(app).AsNoTracking().SingleAsync(item => item.Id == submissionId, Ct);
        return new SubmissionState(row.JsonData, row.IsComplete, row.Status.Code, row.Token?.Value, row.Token?.ExpiresAt);
    }
}
