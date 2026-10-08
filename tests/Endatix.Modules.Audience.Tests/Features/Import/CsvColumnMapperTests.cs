using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class CsvColumnMapperTests
{
    private static readonly IReadOnlyList<Property> Department =
    [
        new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "Department",
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0)),
    ];

    private static readonly IReadOnlyList<string> Headers = ["email", "Dept"];

    [Fact]
    public void MappingError_KnownPropertyAndColumn_ReturnsNull()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["department"] = "dept" };

        // Act
        string? error = CsvColumnMapper.MappingError(Department, Headers, columns);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void MappingError_UnknownProperty_ReturnsMessage()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["departmnet"] = "Dept" };

        // Act
        string? error = CsvColumnMapper.MappingError(Department, Headers, columns);

        // Assert
        error.Should().Be("This form has no audience property 'departmnet'.");
    }

    [Fact]
    public void MappingError_ColumnMissingFromFile_ReturnsMessage()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["department"] = "Team" };

        // Act
        string? error = CsvColumnMapper.MappingError(Department, Headers, columns);

        // Assert
        error.Should().Be("The CSV has no 'Team' column.");
    }
}
