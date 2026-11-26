using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// The webhooks section: the form's own webhook settings, the document <c>Forms.WebHookSettingsJson</c> holds, which
/// moves here (expand: both are written until the column is dropped, endatix/endatix#1784).
/// </summary>
public sealed class FormSettingsWebHooksTests
{
    private const string HOOK_URL = "https://hooks.example.test/completed";
    private const string NEWER_HOOK_URL = "https://hooks.example.test/newer";

    // As Form.UpdateWebHookSettings stores it, and as the migration copies it: Pascal case.
    private const string STORED_BY_FORM =
        """{"Events":{"SubmissionCompleted":{"IsEnabled":true,"WebHookEndpoints":[{"Url":"https://hooks.example.test/completed","Authentication":null}]}}}""";

    private static readonly DateTime _at = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_HasNoWebHooks()
    {
        var settings = NewSettings();

        settings.WebHooksJson.Should().BeNull();
        settings.WebHooks.Events.Should().BeEmpty();
    }

    [Fact]
    public void WebHooks_StoredInPascalCase_ReadsTheEvents()
    {
        var settings = Stored(STORED_BY_FORM);

        var completed = settings.WebHooks.Events["SubmissionCompleted"];
        completed.IsEnabled.Should().BeTrue();
        completed.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public void WebHooks_Unreadable_ReadsAsNoneAndIsNamed()
    {
        var settings = Stored("""{"events":[]}""");

        settings.WebHooks.Events.Should().BeEmpty();
        settings.UnreadableSections().Should().Equal("webHooks");
    }

    [Fact]
    public void Apply_TheSameWebHooksAgain_IsANoOp()
    {
        // A section holding a dictionary: equal content is never equal by reference, so Apply compares JSON.
        var form = NewForm();
        var settings = FormSettings.For(form);
        settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));

        var retry = settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().BeFalse();
        settings.Version.Should().Be(1);
    }

    [Fact]
    public void Apply_WebHooksAndPdf_ReplacesBothInOneVersion()
    {
        var form = NewForm();
        var settings = FormSettings.For(form);
        var change = new FormSettingsChange { WebHooks = Config(), Pdf = new PdfSettings { UsesFormTheme = true } };

        settings.Apply(change, form, new(FormSettings.UNSAVED_VERSION, _at)).Value.Should().BeTrue();

        settings.Version.Should().Be(1);
        settings.Pdf.UsesFormTheme.Should().BeTrue();
        settings.WebHooks.Events.Should().ContainKey("SubmissionCompleted");
    }

    [Fact]
    public void Apply_NewWebHooks_WritesTheFormToo()
    {
        var form = NewForm();
        var settings = FormSettings.For(form);

        settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));

        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public void Apply_TheStaleSectionWhenTheColumnIsAhead_ReplacesItAndWritesTheColumn()
    {
        // An older instance wrote the column alone; readers see the column, so the section's settings are a change.
        var form = NewForm();
        form.UpdateWebHookSettings(Config(NEWER_HOOK_URL));
        var settings = Stored(CamelCase(Config()));

        var applied = settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));

        applied.Value.Should().BeTrue();
        settings.Version.Should().Be(1);
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
        settings.WebHooksOf(form)!.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public void Apply_TheStaleSectionWhenTheColumnWasClearedAlone_ReplacesItAndWritesTheColumn()
    {
        var form = NewForm();
        var settings = Stored(CamelCase(Config()));

        var applied = settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));

        applied.Value.Should().BeTrue();
        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
    }

    [Fact]
    public void Apply_TheStaleSectionWhenTheColumnIsAhead_FromAStaleVersion_IsAConflict()
    {
        var form = NewForm();
        form.UpdateWebHookSettings(Config(NEWER_HOOK_URL));
        var settings = Stored(CamelCase(Config()));

        var applied = settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(7, _at));

        applied.Status.Should().Be(ResultStatus.Conflict);
        settings.Version.Should().Be(FormSettings.UNSAVED_VERSION);
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(NEWER_HOOK_URL);
    }

    [Fact]
    public void Apply_TheColumnsSettingsWhenTheColumnIsAhead_IsANoOpWhateverTheVersion()
    {
        // What readers see is sent back: nothing changes, as for any other value equal to what is stored.
        var form = NewForm();
        form.UpdateWebHookSettings(Config(NEWER_HOOK_URL));
        var settings = Stored(CamelCase(Config()));

        var applied = settings.Apply(new FormSettingsChange { WebHooks = Config(NEWER_HOOK_URL) }, form, new(7, _at));

        applied.IsSuccess.Should().BeTrue();
        applied.Value.Should().BeFalse();
        settings.Version.Should().Be(FormSettings.UNSAVED_VERSION);
    }

    [Fact]
    public void Apply_ToAnotherForm_Throws()
    {
        var apply = () => NewSettings().Apply(new FormSettingsChange { WebHooks = Config() }, Form.Create(3, new FormCreateArgs(TenantId: 1, Name: "Other")), new(0, _at));

        apply.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CopyWebHooksFrom_AFormWithWebHooks_StoresItsDocumentAsIsAndBumpsTheVersion()
    {
        var form = NewForm();
        form.UpdateWebHookSettings(Config());
        var settings = FormSettings.For(form);

        var copied = settings.CopyWebHooksFrom(form, _at);

        copied.Should().BeTrue();
        settings.WebHooksJson.Should().Be(form.WebHookSettingsJson);
        settings.Version.Should().Be(1);
        settings.ModifiedAt.Should().Be(_at);
    }

    [Fact]
    public void CopyWebHooksFrom_TheSameDocumentAgain_ChangesNothing()
    {
        var form = NewForm();
        form.UpdateWebHookSettings(Config());
        var settings = FormSettings.For(form);
        settings.CopyWebHooksFrom(form, _at);

        settings.CopyWebHooksFrom(form, _at.AddMinutes(1)).Should().BeFalse();

        settings.Version.Should().Be(1);
        settings.ModifiedAt.Should().Be(_at);
    }

    [Fact]
    public void CopyWebHooksFrom_AFormWhoseWebHooksWereCleared_ClearsTheSection()
    {
        var form = NewForm();
        form.UpdateWebHookSettings(Config());
        var settings = FormSettings.For(form);
        settings.CopyWebHooksFrom(form, _at);
        form.UpdateWebHookSettings(null);

        settings.CopyWebHooksFrom(form, _at).Should().BeTrue();

        settings.WebHooksJson.Should().BeNull();
        settings.Version.Should().Be(2);
    }

    [Fact]
    public void CopyWebHooksFrom_AFormWithoutWebHooks_ToNoSection_ChangesNothing()
    {
        var form = NewForm();
        var settings = FormSettings.For(form);

        settings.CopyWebHooksFrom(form, _at).Should().BeFalse();

        settings.Version.Should().Be(FormSettings.UNSAVED_VERSION);
    }

    [Fact]
    public void CopyWebHooksFrom_AnotherForm_Throws()
    {
        var settings = NewSettings();
        var other = Form.Create(3, new FormCreateArgs(TenantId: 1, Name: "Other"));

        var copy = () => settings.CopyWebHooksFrom(other, _at);

        copy.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_TheSettingsOfAPascalCaseCopy_IsANoOp()
    {
        // The migration copies the column in Pascal case; a settings write stores camelCase. Same settings, no change.
        var form = NewForm();
        form.UpdateWebHookSettings(Config());
        var settings = Stored(STORED_BY_FORM);

        var applied = settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(7, _at));

        applied.IsSuccess.Should().BeTrue();
        applied.Value.Should().BeFalse();
        settings.WebHooksJson.Should().Be(STORED_BY_FORM);
    }

    [Fact]
    public void CopyWebHooksFrom_TheSameSettingsInAnotherCasing_ChangesNothing()
    {
        // A settings write stored camelCase and wrote the column in Pascal case; a later form save copies it back.
        var form = NewForm();
        var settings = FormSettings.For(form);
        settings.Apply(new FormSettingsChange { WebHooks = Config() }, form, new(FormSettings.UNSAVED_VERSION, _at));
        form.UpdateWebHookSettings(Config());

        settings.CopyWebHooksFrom(form, _at.AddMinutes(1)).Should().BeFalse();

        settings.Version.Should().Be(1);
    }

    [Fact]
    public void WebHooksOf_AFormWithoutWebHooks_IsNullWhateverTheSectionHolds()
    {
        // An older instance cleared the form's webhooks after the copy: its tenant's apply.
        var settings = Stored(STORED_BY_FORM);

        settings.WebHooksOf(NewForm()).Should().BeNull();
    }

    [Fact]
    public void WebHooksOf_ASectionThatDiffersFromTheColumn_IsTheColumn()
    {
        // An older instance changed the column alone after the copy: the column is the copy every release writes.
        var form = NewForm();
        form.UpdateWebHookSettings(Config(NEWER_HOOK_URL));
        var settings = Stored(STORED_BY_FORM);

        var webHooks = settings.WebHooksOf(form);

        webHooks!.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(NEWER_HOOK_URL);
    }

    [Fact]
    public void WebHooksOf_TheSameSettings_IsTheSection()
    {
        var form = NewForm();
        form.UpdateWebHookSettings(Config());
        var settings = Stored("""{"events":{"SubmissionCompleted":{"isEnabled":true,"webHookEndpoints":[{"url":"https://hooks.example.test/completed"}]}}}""");

        settings.WebHooksOf(form)!.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public void WebHooksOf_AnotherForm_Throws()
    {
        var read = () => NewSettings().WebHooksOf(Form.Create(3, new FormCreateArgs(TenantId: 1, Name: "Other")));

        read.Should().Throw<ArgumentException>();
    }

    private static Form NewForm() => Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form"));

    private static FormSettings NewSettings() => FormSettings.For(NewForm());

    private static FormSettings Stored(string webHooksJson)
    {
        var settings = NewSettings();
        typeof(FormSettings).GetProperty(nameof(FormSettings.WebHooksJson))!.SetValue(settings, webHooksJson);
        return settings;
    }

    // As a settings write stores the section.
    private static string CamelCase(WebHookConfiguration config) => FormSettingsJson.Write(config);

    private static WebHookConfiguration Config(string url = HOOK_URL) => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = url }] },
        },
    };
}
