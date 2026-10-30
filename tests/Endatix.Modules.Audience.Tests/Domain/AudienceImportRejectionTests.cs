using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public sealed class AudienceImportRejectionTests
{
    private static readonly AudienceImport Import = AudienceImport.Start(new AudienceImportStartArgs(
        1, 2, "people.csv", ["email"], "email", new Dictionary<string, string>(), IsTest: false,
        RowCount: 10, ChunkSize: 5, PerformedById: null));

    [Fact]
    public void Create_RejectedRow_KeepsItsRowNumberReasonAndCellsAsUploaded()
    {
        // Arrange
        NumberedRow row = new(7, new StagedRow(["not-an-email", " Sofia "], Malformed: false));

        // Act
        AudienceImportRejection rejection = AudienceImportRejection.Create(Import, row, "The email is not valid.");

        // Assert
        rejection.RowNumber.Should().Be(7);
        rejection.Reason.Should().Be("The email is not valid.");
        rejection.CellsJson.Should().Be("""["not-an-email"," Sofia "]""");
        rejection.TenantId.Should().Be(Import.TenantId);
    }

    [Fact]
    public void Create_LongReason_KeepsTheFirstFiveHundredCharacters()
    {
        // Arrange
        NumberedRow row = new(2, new StagedRow(["ada@example.com"], Malformed: false));

        // Act
        AudienceImportRejection rejection =
            AudienceImportRejection.Create(Import, row, new string('x', AudienceImportRejection.ReasonMaxLength + 1));

        // Assert
        rejection.Reason.Should().HaveLength(AudienceImportRejection.ReasonMaxLength);
    }

    [Fact]
    public void Create_NoReason_IsRefused()
    {
        // Act
        Action act = () => AudienceImportRejection.Create(Import, new NumberedRow(2, new StagedRow([], false)), " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
