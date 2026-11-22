using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormSettings.Get;
using Endatix.Core.UseCases.FormSettings.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.FormSettings;

public sealed class FormSettingsHandlerTests
{
    private const long FORM_ID = 42;
    private static readonly FormSettingsChange _themeOn = new() { Pdf = new PdfSettings { UsesFormTheme = true } };

    private readonly Form _form = Form.Create(FORM_ID, new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Form"));
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IFormSettingsRepository _settings = Substitute.For<IFormSettingsRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public FormSettingsHandlerTests() =>
        _clock.Now.Returns(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Get_MissingForm_IsNotFound()
    {
        var result = await GetHandler().Handle(new GetFormSettingsQuery(FORM_ID), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _settings.DidNotReceiveWithAnyArgs().ReadAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_NoRow_ReturnsVersionZeroAndDefaults()
    {
        StoredForm();
        _settings.ReadAsync(_form, Arg.Any<CancellationToken>())
            .Returns(Core.Entities.FormSettings.For(_form));

        var result = await GetHandler().Handle(new GetFormSettingsQuery(FORM_ID), TestContext.Current.CancellationToken);

        result.Value.Version.Should().Be(0);
        result.Value.Pdf.Should().Be(new PdfSettings());
    }

    [Fact]
    public async Task Update_MissingForm_IsNotFound()
    {
        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Update_NoRow_StartsOneAndSavesIt()
    {
        StoredForm();

        _settings.SaveOrConcedeAsync(Arg.Any<Core.Entities.FormSettings>(), Arg.Any<CancellationToken>()).Returns(true);
        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Value.Version.Should().Be(1);
        result.Value.Pdf.UsesFormTheme.Should().BeTrue();
        await _settings.Received(1).SaveOrConcedeAsync(
            Arg.Is<Core.Entities.FormSettings>(s => s.FormId == FORM_ID && s.TenantId == SampleData.TENANT_ID),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_NoChange_SavesNothing()
    {
        StoredForm();
        var stored = Core.Entities.FormSettings.For(_form);
        stored.Apply(_themeOn, _form, 0, DateTime.UtcNow);
        _settings.FindAsync(FORM_ID, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Value.Version.Should().Be(1);
        await _settings.DidNotReceiveWithAnyArgs().SaveOrConcedeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Update_NoSectionOnAMissingRow_InsertsNothing()
    {
        StoredForm();

        var result = await UpdateHandler().Handle(
            new PartialUpdateFormSettingsCommand(FORM_ID, 0, new FormSettingsChange()),
            TestContext.Current.CancellationToken);

        result.Value.Version.Should().Be(0);
        await _settings.DidNotReceiveWithAnyArgs().SaveOrConcedeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Update_AConcededSave_IsAConflict()
    {
        StoredForm();
        _settings.SaveOrConcedeAsync(Arg.Any<Core.Entities.FormSettings>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Update_LosingARaceToTheSameValue_IsANoOp()
    {
        StoredForm();
        var winner = Core.Entities.FormSettings.For(_form);
        winner.Apply(_themeOn, _form, 0, DateTime.UtcNow);
        _settings.FindAsync(FORM_ID, Arg.Any<CancellationToken>()).Returns(null, winner);
        _settings.SaveOrConcedeAsync(Arg.Any<Core.Entities.FormSettings>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
    }

    [Fact]
    public async Task Update_LosingARaceToADifferentValue_IsAConflict()
    {
        StoredForm();
        var winner = Core.Entities.FormSettings.For(_form);
        winner.Apply(new FormSettingsChange { Pdf = new PdfSettings() }, _form, 0, DateTime.UtcNow);
        _settings.FindAsync(FORM_ID, Arg.Any<CancellationToken>()).Returns(null, winner);
        _settings.SaveOrConcedeAsync(Arg.Any<Core.Entities.FormSettings>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Update_AStaleVersion_IsAConflictAndSavesNothing()
    {
        StoredForm();
        var stored = Core.Entities.FormSettings.For(_form);
        stored.Apply(new FormSettingsChange { Pdf = new PdfSettings() }, _form, 0, DateTime.UtcNow);
        _settings.FindAsync(FORM_ID, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await UpdateHandler().Handle(Command(0), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Conflict);
        await _settings.DidNotReceiveWithAnyArgs().SaveOrConcedeAsync(default!, TestContext.Current.CancellationToken);
    }

    private static PartialUpdateFormSettingsCommand Command(long expectedVersion) => new(FORM_ID, expectedVersion, _themeOn);

    private GetFormSettingsHandler GetHandler() => new(_forms, _settings);

    private PartialUpdateFormSettingsHandler UpdateHandler() => new(_forms, _settings, _clock);

    private void StoredForm() =>
        _forms.GetByIdAsync(FORM_ID, Arg.Any<CancellationToken>()).Returns(_form);
}
