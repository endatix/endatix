using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.Entities;

public sealed class FormSettingsTests
{
    private static readonly DateTime _at = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FormSettingsChange _themeOn = new() { Pdf = new PdfSettings { UsesFormTheme = true } };

    [Fact]
    public void New_HasEverySectionAtItsDefaults()
    {
        var settings = NewSettings();

        settings.Version.Should().Be(FormSettings.UNSAVED_VERSION);
        settings.PdfJson.Should().BeNull();
        settings.Pdf.Should().Be(new PdfSettings());
        settings.ModifiedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"usesFormTheme":true,"future":1}""")]
    public void Pdf_StoredJson_IgnoresUnknownFieldsAndDefaultsMissingOnes(string stored)
    {
        var settings = Stored(stored);

        settings.Pdf.UsesFormTheme.Should().Be(stored.Contains("true", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"usesFormTheme":"yes"}""")]
    [InlineData("not json")]
    public void Pdf_UnreadableStoredJson_ReadsAsTheDefaultsAndAWriteReplacesIt(string stored)
    {
        var settings = Stored(stored);

        settings.Pdf.Should().Be(new PdfSettings());
        settings.Apply(_themeOn, 0, _at).Value.Should().BeTrue();
        settings.Pdf.UsesFormTheme.Should().BeTrue();
    }

    [Fact]
    public void Apply_ANewValue_ReplacesTheSectionAndBumpsTheVersion()
    {
        var settings = NewSettings();

        var applied = settings.Apply(_themeOn, FormSettings.UNSAVED_VERSION, _at);

        applied.Value.Should().BeTrue();
        settings.Version.Should().Be(1);
        settings.Pdf.UsesFormTheme.Should().BeTrue();
        settings.PdfJson.Should().Be("""{"usesFormTheme":true}""");
        settings.ModifiedAt.Should().Be(_at);
    }

    [Fact]
    public void Apply_TheStoredValueAgain_IsANoOpWhateverTheVersion()
    {
        var settings = NewSettings();
        settings.Apply(_themeOn, 0, _at);

        var retry = settings.Apply(_themeOn, expectedVersion: 0, _at.AddMinutes(1));

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().BeFalse();
        settings.Version.Should().Be(1);
        settings.ModifiedAt.Should().Be(_at);
    }

    [Fact]
    public void Apply_TheStoredValue_AsJsonbReturnsIt_IsANoOp()
    {
        // jsonb gives the value back with its own spacing and key order.
        var settings = Stored("""{"usesFormTheme": true}""");

        var applied = settings.Apply(_themeOn, 0, _at);

        applied.Value.Should().BeFalse();
    }

    [Fact]
    public void Apply_ADifferentValueFromAStaleVersion_IsAConflict()
    {
        var settings = NewSettings();
        settings.Apply(_themeOn, 0, _at);

        var stale = settings.Apply(new FormSettingsChange { Pdf = new PdfSettings() }, expectedVersion: 0, _at);

        stale.Status.Should().Be(ResultStatus.Conflict);
        settings.Version.Should().Be(1);
        settings.Pdf.UsesFormTheme.Should().BeTrue();
    }

    [Fact]
    public void Apply_AnExplicitDefault_IsStored()
    {
        var settings = NewSettings();

        var applied = settings.Apply(new FormSettingsChange { Pdf = new PdfSettings() }, 0, _at);

        applied.Value.Should().BeTrue();
        settings.PdfJson.Should().Be("""{"usesFormTheme":false}""");
    }

    [Fact]
    public void Apply_NoSection_ChangesNothing()
    {
        var settings = NewSettings();

        settings.Apply(new FormSettingsChange(), 0, _at).Value.Should().BeFalse();
        settings.Version.Should().Be(FormSettings.UNSAVED_VERSION);
    }

    [Fact]
    public void For_TakesTheTenantAndIdFromTheForm()
    {
        var settings = FormSettings.For(Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form")));

        settings.TenantId.Should().Be(1);
        settings.FormId.Should().Be(2);
    }

    [Fact]
    public void For_AnUnsavedForm_Throws()
    {
        var unsaved = Form.Create(new FormCreateArgs(TenantId: 1, Name: "Form"));

        var start = () => FormSettings.For(unsaved);

        start.Should().Throw<ArgumentException>();
    }

    private static FormSettings NewSettings() => FormSettings.For(Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form")));

    private static FormSettings Stored(string pdfJson)
    {
        var settings = NewSettings();
        typeof(FormSettings).GetProperty(nameof(FormSettings.PdfJson))!.SetValue(settings, pdfJson);
        return settings;
    }
}
