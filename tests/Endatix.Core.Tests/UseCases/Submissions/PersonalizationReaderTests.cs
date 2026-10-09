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
    public void Read_TypedVariables_RoundTripsEdacAsNumber()
    {
        const string snapshot = """
            {"schemaVersion":1,"capturedAt":"2026-10-08T08:00:00Z","identifier":"0570123456-01","variables":{"ciudad":"03. ANTIOQUIA - MEDELLIN","edac":65}}
            """;

        PersonalizationRead? read = PersonalizationReader.Read(snapshot);

        read.Should().NotBeNull();
        read!.Identifier.Should().Be("0570123456-01");
        read.Variables.GetProperty("edac").GetDouble().Should().Be(65);
        read.Variables.GetProperty("ciudad").GetString().Should().Be("03. ANTIOQUIA - MEDELLIN");
        read.Source.Should().BeNull();
        read.MemberId.Should().BeNull();
    }

    [Fact]
    public void Read_OnBehalf_ReturnsSourceAndMember()
    {
        const string snapshot = """
            {"schemaVersion":1,"source":"on_behalf","memberId":42,"capturedAt":"2026-10-08T08:00:00Z","identifier":"0570123456-01","variables":{"edac":65}}
            """;

        PersonalizationRead? read = PersonalizationReader.Read(snapshot);

        read!.Source.Should().Be("on_behalf");
        read.MemberId.Should().Be(42);
        read.Variables.GetProperty("edac").GetDouble().Should().Be(65);
    }
}
