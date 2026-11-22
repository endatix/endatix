using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// The webhooks section: the form's own webhook settings, the document <c>Forms.WebHookSettingsJson</c> holds, which
/// moves here (expand: both are written until the column is dropped, endatix/endatix#1784).
/// </summary>
public sealed class FormSettingsWebHooksTests
{
    private const string HOOK_URL = "https://hooks.example.test/completed";

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
        var settings = NewSettings();
        settings.Apply(new FormSettingsChange { WebHooks = Config() }, FormSettings.UNSAVED_VERSION, _at);

        var retry = settings.Apply(new FormSettingsChange { WebHooks = Config() }, FormSettings.UNSAVED_VERSION, _at);

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().BeFalse();
        settings.Version.Should().Be(1);
    }

    [Fact]
    public void Apply_WebHooksAndPdf_ReplacesBothInOneVersion()
    {
        var settings = NewSettings();
        var change = new FormSettingsChange { WebHooks = Config(), Pdf = new PdfSettings { UsesFormTheme = true } };

        settings.Apply(change, FormSettings.UNSAVED_VERSION, _at).Value.Should().BeTrue();

        settings.Version.Should().Be(1);
        settings.Pdf.UsesFormTheme.Should().BeTrue();
        settings.WebHooks.Events.Should().ContainKey("SubmissionCompleted");
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

    private static Form NewForm() => Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form"));

    private static FormSettings NewSettings() => FormSettings.For(NewForm());

    private static FormSettings Stored(string webHooksJson)
    {
        var settings = NewSettings();
        typeof(FormSettings).GetProperty(nameof(FormSettings.WebHooksJson))!.SetValue(settings, webHooksJson);
        return settings;
    }

    private static WebHookConfiguration Config() => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = HOOK_URL }] },
        },
    };
}
