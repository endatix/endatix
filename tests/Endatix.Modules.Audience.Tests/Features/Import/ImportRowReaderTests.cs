using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class ImportRowReaderTests
{
    private static readonly CsvColumnMap EmailOnly = CsvColumnMapper.Build(new CsvColumnRequest([], ["email"], "email", null));
    private static readonly IValueNormalizer Upper = new UpperInvariantNormalizer();
    private static readonly ImportMatchKey EmailKey = new(AudienceIdentifierKindCodes.Email, Upper);
    private static readonly ImportMatchKey ExternalIdKey = new(AudienceIdentifierKindCodes.ExternalId, Upper);

    [Fact]
    public void Read_DuplicateIdentifier_RejectsLaterRowWithFileRowNumber()
    {
        // Arrange
        List<CsvDataRow> rows =
        [
            Row(2, "ada@example.com"),
            Row(3, " ADA@example.com "),
        ];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, EmailKey);

        // Assert
        ImportRow accepted = result.Accepted.Should().ContainSingle().Subject;
        accepted.Identifier.Should().Be("ada@example.com");
        accepted.NormalizedIdentifier.Should().Be("ADA@EXAMPLE.COM");
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(3, "Identifier appears more than once in the file."));
    }

    [Fact]
    public void Read_BlankIdentifier_RejectsRow()
    {
        // Arrange
        List<CsvDataRow> rows = [Row(2, "   ")];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, EmailKey);

        // Assert
        result.Accepted.Should().BeEmpty();
        result.Rejected.Should().ContainSingle().Which.Reason.Should().Be("Identifier is required.");
    }

    [Fact]
    public void Read_ExternalIdsDifferingInCase_AcceptsBoth()
    {
        // Arrange
        List<CsvDataRow> rows = [Row(2, "00Qx7"), Row(3, "00QX7")];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, ExternalIdKey);

        // Assert
        result.Accepted.Select(row => row.Identifier).Should().Equal("00Qx7", "00QX7");
        result.Rejected.Should().BeEmpty();
    }

    [Fact]
    public void Read_IdentifierLongerThanTheColumn_RejectsRow()
    {
        // Arrange
        string identifier = new string('a', 251) + "@x.com";
        List<CsvDataRow> rows = [Row(2, identifier)];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, EmailKey);

        // Assert
        result.Accepted.Should().BeEmpty();
        result.Rejected.Should().ContainSingle().Which.RowNumber.Should().Be(2);
    }

    [Fact]
    public void Read_InvalidEmail_RejectsRowWithReason()
    {
        // Arrange
        List<CsvDataRow> rows = [Row(4, "John Smith")];

        // Act
        ImportRows result = ImportRowReader.Read(rows, EmailOnly, EmailKey);

        // Assert
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(4, "'John Smith' is not a valid email address."));
    }

    [Fact]
    public void Read_ValueOfWrongType_RejectsRowWithReason()
    {
        // Arrange
        CsvColumnMap map = AgeMap();
        CsvDataRow row = new(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = "ada@example.com",
            ["Age"] = "forty",
        });

        // Act
        ImportRows result = ImportRowReader.Read([row], map, EmailKey);

        // Assert
        result.Accepted.Should().BeEmpty();
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(2, "'Age' must be a number."));
    }

    [Fact]
    public void Read_RowRejectedForAValue_DoesNotMakeALaterRowForThePersonADuplicate()
    {
        // Arrange
        CsvDataRow bad = AgeRow(2, "ada@example.com", "forty");
        CsvDataRow good = AgeRow(5, "ada@example.com", "40");

        // Act
        ImportRows result = ImportRowReader.Read([bad, good], AgeMap(), EmailKey);

        // Assert
        result.Accepted.Should().ContainSingle().Which.Values.Should().ContainValue("40");
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(2, "'Age' must be a number."));
    }

    [Fact]
    public void Read_MalformedRow_RejectsItAndKeepsTheRest()
    {
        // Arrange
        CsvDataRow malformed = Row(2, "ada@example.com") with { Malformed = true };

        // Act
        ImportRows result = ImportRowReader.Read([malformed, Row(3, "bo@example.com")], EmailOnly, EmailKey);

        // Assert
        result.Accepted.Should().ContainSingle().Which.Identifier.Should().Be("bo@example.com");
        result.Rejected.Should().ContainSingle().Which.RowNumber.Should().Be(2);
    }

    private static CsvColumnMap AgeMap()
    {
        Property age = new(new PropertyCreateArgs(1, 10, "Age", AudienceDataTypeCodes.Number, 0)) { Id = 7 };
        return CsvColumnMapper.Build(new CsvColumnRequest(
            [age], ["email", "Age"], "email", new Dictionary<string, string> { ["age"] = "Age" }));
    }

    private static CsvDataRow AgeRow(int rowNumber, string email, string age) =>
        new(rowNumber, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = email,
            ["Age"] = age,
        });

    [Fact]
    public void Start_RepeatInALaterPart_RejectsItAsADuplicate()
    {
        // Arrange
        ImportRowReader.RowSorter reader = ImportRowReader.Start(EmailOnly, EmailKey);
        reader.ReadPart([Row(2, "ada@example.com")]);

        // Act
        ImportRows later = reader.ReadPart([Row(1_002, "ADA@example.com"), Row(1_003, "cy@example.com")]);

        // Assert
        later.Accepted.Select(row => row.RowNumber).Should().Equal(1_003);
        later.Rejected.Should().ContainSingle()
            .Which.Should().Be(new ImportRejectionDto(1_002, "Identifier appears more than once in the file."));
    }

    [Fact]
    public void Start_RowRejectedInAnEarlierPart_DoesNotClaimItsIdentifier()
    {
        // Arrange — a stray quote rejects the first row, so its identifier stays free.
        ImportRowReader.RowSorter reader = ImportRowReader.Start(EmailOnly, EmailKey);
        ImportRows first = reader.ReadPart([Row(2, "ada@example.com") with { Malformed = true }]);

        // Act
        ImportRows later = reader.ReadPart([Row(1_002, "ada@example.com")]);

        // Assert
        first.Rejected.Should().ContainSingle();
        later.Accepted.Should().ContainSingle().Which.RowNumber.Should().Be(1_002);
        later.Rejected.Should().BeEmpty();
    }

    private static CsvDataRow Row(int rowNumber, string email) =>
        new(rowNumber, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["email"] = email });

    private sealed class UpperInvariantNormalizer : IValueNormalizer
    {
        public string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }
}
