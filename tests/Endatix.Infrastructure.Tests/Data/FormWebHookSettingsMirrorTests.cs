using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// The mirror copies whichever of a form's two webhook copies a save changed into the other, on tracked entries. The
/// paths that read a row from the database are covered by <c>FormWebHookSettingsDualWriteTests</c>.
/// </summary>
public sealed class FormWebHookSettingsMirrorTests : IDisposable
{
    private const string HOOK_URL = "https://hooks.example.test/completed";
    private const string OTHER_HOOK_URL = "https://hooks.example.test/other";

    private static readonly DateTime _at = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

    [Fact]
    public void Apply_ANewFormWithWebHooks_AddsItsSettingsWithTheCopy()
    {
        var form = NewForm(Config(HOOK_URL));
        _context.Add(form);

        FormWebHookSettingsMirror.Apply(_context, _at);

        var settings = _context.FormSettings.Local.Should().ContainSingle().Subject;
        settings.WebHooksJson.Should().Be(form.WebHookSettingsJson);
        settings.Version.Should().Be(1);
    }

    [Fact]
    public void Apply_ANewFormWithoutWebHooks_AddsNoSettings()
    {
        _context.Add(NewForm(null));

        FormWebHookSettingsMirror.Apply(_context, _at);

        _context.FormSettings.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_AChangedColumn_IsCopiedIntoTheTrackedSection()
    {
        var (form, settings) = Stored(Config(HOOK_URL));
        form.UpdateWebHookSettings(Config(OTHER_HOOK_URL));

        await FormWebHookSettingsMirror.ApplyAsync(_context, _at, TestContext.Current.CancellationToken);

        settings.WebHooks.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(OTHER_HOOK_URL);
        settings.Version.Should().Be(2);
    }

    [Fact]
    public void Apply_AWholeFormUpdateWithTheSameColumn_LeavesTheSectionAlone()
    {
        var (form, settings) = Stored(Config(HOOK_URL));
        _context.Update(form);

        FormWebHookSettingsMirror.Apply(_context, _at);

        settings.Version.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_ASectionChangedWithoutItsForm_IsWrittenToTheTrackedForm()
    {
        // The settings write was given another copy of the form, so the tracked one did not change.
        var (form, settings) = Stored(Config(HOOK_URL));
        var copy = Form.Create(form.Id, new FormCreateArgs(TenantId: form.TenantId, Name: "copy", WebHookSettingsJson: form.WebHookSettingsJson));
        settings.Apply(new FormSettingsChange { WebHooks = Config(OTHER_HOOK_URL) }, copy, new FormSettingsWrite(1, _at));

        await FormWebHookSettingsMirror.ApplyAsync(_context, _at, TestContext.Current.CancellationToken);

        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(OTHER_HOOK_URL);
    }

    [Fact]
    public void Apply_ANewSectionForAnUnchangedForm_IsWrittenToTheTrackedForm()
    {
        var form = NewForm(null);
        _context.Attach(form);
        var copy = Form.Create(form.Id, new FormCreateArgs(TenantId: form.TenantId, Name: "copy"));
        var settings = FormSettings.For(form);
        settings.Apply(new FormSettingsChange { WebHooks = Config(HOOK_URL) }, copy, new FormSettingsWrite(FormSettings.UNSAVED_VERSION, _at));
        _context.Add(settings);

        FormWebHookSettingsMirror.Apply(_context, _at);

        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
    }

    public void Dispose() => _context.Dispose();

    // A form and its settings holding the same webhooks, as loaded from the database.
    private (Form Form, FormSettings Settings) Stored(WebHookConfiguration webHooks)
    {
        var form = NewForm(webHooks);
        var settings = FormSettings.For(form);
        settings.CopyWebHooksFrom(form, _at);
        _context.Attach(form);
        _context.Attach(settings);
        return (form, settings);
    }

    private static Form NewForm(WebHookConfiguration? webHooks)
    {
        var form = Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form"));
        form.UpdateWebHookSettings(webHooks);
        return form;
    }

    private static WebHookConfiguration Config(string url) => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = url }] },
        },
    };
}
