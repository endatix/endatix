using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Import;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class ImportRowReaderTests
{
    private static readonly CsvColumnMap EmailOnly = CsvColumnMapper.Build([], "email", null);

    [Fact]
    public void Read_DuplicateIdentifier_RejectsLaterRowWithFileRowNumber()
    {
        // Arrange
        List<IReadOnlyDictionary<string, string>> rows =
        [
            Row("ada@example.com"),
            Row(" ADA@example.com "),
        ];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, AudienceIdentifierKindCodes.Email);

        // Assert
        result.Accepted.Should().ContainSingle().Which.Identifier.Should().Be("ada@example.com");
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(3, "Identifier appears more than once in the file."));
    }

    [Fact]
    public void Read_BlankIdentifier_RejectsRow()
    {
        // Arrange
        List<IReadOnlyDictionary<string, string>> rows = [Row("   ")];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, AudienceIdentifierKindCodes.Email);

        // Assert
        result.Accepted.Should().BeEmpty();
        result.Rejected.Should().ContainSingle().Which.Reason.Should().Be("Identifier is required.");
    }

    [Fact]
    public void Read_ExternalIdsDifferingInCase_AcceptsBoth()
    {
        // Arrange
        List<IReadOnlyDictionary<string, string>> rows = [Row("00Qx7"), Row("00QX7")];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, AudienceIdentifierKindCodes.ExternalId);

        // Assert
        result.Accepted.Select(row => row.Identifier).Should().Equal("00Qx7", "00QX7");
        result.Rejected.Should().BeEmpty();
    }

    private static IReadOnlyDictionary<string, string> Row(string email) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["email"] = email };
}
