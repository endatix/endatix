using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public sealed class AudienceImportOptionsTests
{
    [Fact]
    public void Start_WithOptions_KeepsThemThroughAResume()
    {
        // Arrange
        AudienceImportOptions options = new(ClearEmptyCells: true, ClearMissingProperties: false);
        AudienceImport import = AudienceImport.Start(StartArgs(options));
        import.BeginCheck();
        import.FinishCheck();
        import.Confirm();
        import.Fail("The import stopped.", resumable: true);

        // Act
        import.Resume();

        // Assert
        import.Options.Should().Be(new AudienceImportOptions(ClearEmptyCells: true, ClearMissingProperties: false));
    }

    [Fact]
    public void Start_WithoutOptions_KeepsEveryStoredValue()
    {
        // Act
        AudienceImport import = AudienceImport.Start(StartArgs(options: null));

        // Assert
        import.Options.Should().Be(AudienceImportOptions.Keep);
    }

    private static AudienceImportStartArgs StartArgs(AudienceImportOptions? options) =>
        new(1, 2, "people.csv", ["email"], "email", new Dictionary<string, string>(), IsTest: false,
            RowCount: 1_200, ChunkSize: 1_000, PerformedById: null, Options: options);
}
