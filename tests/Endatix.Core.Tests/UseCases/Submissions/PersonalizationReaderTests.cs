using Endatix.Core.UseCases.Submissions;

namespace Endatix.Core.Tests.UseCases.Submissions;

public sealed class PersonalizationReaderTests
{
    [Fact]
    public void Read_BlankOrAnonymous_ReturnsNull()
    {
        PersonalizationReader.Read(null).Should().BeNull();
        PersonalizationReader.Read("").Should().BeNull();
    }

    [Fact]
    public void Read_TypedVariables_RoundTripsAgeAsNumber()
    {
        const string snapshot = """
            {"schemaVersion":1,"capturedAt":"2026-10-08T08:00:00Z","identifier":"CG-000001-01","variables":{"city":"03. MIDWEST - COLUMBUS","age":65}}
            """;

        PersonalizationRead? read = PersonalizationReader.Read(snapshot);

        read.Should().NotBeNull();
        read!.Identifier.Should().Be("CG-000001-01");
        read.Variables.GetProperty("age").GetDouble().Should().Be(65);
        read.Variables.GetProperty("city").GetString().Should().Be("03. MIDWEST - COLUMBUS");
    }
}
