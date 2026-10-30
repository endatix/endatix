using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Data;

public class SqlIdentifierTests
{
    [Theory]
    [InlineData("export_form_submissions")]
    [InlineData("Export_Rows2")]
    [InlineData("reporting.export_rows")]
    public void EnsureValid_PlainIdentifier_ReturnsTheName(string name)
    {
        // Act
        var result = SqlIdentifier.EnsureValid(name);

        // Assert
        result.Should().Be(name);
    }

    [Theory]
    [InlineData("export(1); DROP TABLE submissions; --")]
    [InlineData("export rows")]
    [InlineData("\"quoted\"")]
    [InlineData("a.b.c")]
    [InlineData("1export")]
    [InlineData("export;")]
    [InlineData(" ")]
    [InlineData("export_form_submissions\n")]
    [InlineData("export_form_submissions\r\n")]
    public void EnsureValid_AnythingButAnIdentifier_Throws(string name)
    {
        // Act
        var ensure = () => SqlIdentifier.EnsureValid(name);

        // Assert
        ensure.Should().Throw<ArgumentException>();
    }
}
