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

        var read = PersonalizationReader.Read(snapshot);

        read.Should().NotBeNull();
        read!.Identifier.Should().Be("CG-000001-01");
        read.Variables.GetProperty("age").GetDouble().Should().Be(65);
        read.Variables.GetProperty("city").GetString().Should().Be("03. MIDWEST - COLUMBUS");
        read.Source.Should().BeNull();
        read.MemberId.Should().BeNull();
    }

    [Fact]
    public void Read_OnBehalf_ReturnsSourceAndMember()
    {
        const string snapshot = """
            {"schemaVersion":1,"source":"on_behalf","memberId":42,"capturedAt":"2026-10-08T08:00:00Z","identifier":"CG-000001-01","variables":{"age":65}}
            """;

        PersonalizationRead? read = PersonalizationReader.Read(snapshot);

        read!.Source.Should().Be("on_behalf");
        read.MemberId.Should().Be(42);
        read.Variables.GetProperty("age").GetDouble().Should().Be(65);
    }

    [Theory]
    [InlineData("""{"identifier":123,"capturedAt":1728374400,"variables":{"age":65}}""")]
    [InlineData("""{"identifier":null,"capturedAt":"not a date","variables":{"age":65}}""")]
    public void Read_FieldsOfAnUnexpectedType_ReadAsDefaultsInsteadOfThrowing(string snapshot)
    {
        // Act
        var read = PersonalizationReader.Read(snapshot);

        // Assert
        read.Should().NotBeNull();
        read!.Identifier.Should().BeEmpty();
        read.CapturedAt.Should().Be(default);
        read.Variables.GetProperty("age").GetInt32().Should().Be(65);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"variables":[]}""")]
    public void Read_NotASnapshot_ReturnsNull(string snapshot)
    {
        PersonalizationReader.Read(snapshot).Should().BeNull();
    }

    [Theory]
    [InlineData("""{"memberId":"123","variables":{}}""")]
    [InlineData("""{"memberId":null,"variables":{}}""")]
    [InlineData("""{"memberId":{"id":1},"variables":{}}""")]
    public void Read_MemberIdThatIsNotANumber_ReadsAsNoMemberInsteadOfThrowing(string snapshot)
    {
        // Act
        var read = PersonalizationReader.Read(snapshot);

        // Assert
        read.Should().NotBeNull();
        read!.MemberId.Should().BeNull();
    }

    [Fact]
    public void Read_SnapshotWithValues_ReturnsEachPropertyNameAndDataType()
    {
        // Arrange
        const string snapshot = """
            {"identifier":"CG-1","variables":{"city":"Columbus","age":65},
             "values":[{"propertyId":1,"variableName":"city","name":"City","dataType":"text","value":"Columbus"},
                       {"propertyId":2,"variableName":"age","name":"Age","dataType":"number","value":65}]}
            """;

        // Act
        var read = PersonalizationReader.Read(snapshot);

        // Assert
        read!.Values.Should().Equal(
            new PersonalizationValue("city", "City", "text"),
            new PersonalizationValue("age", "Age", "number"));
    }

    [Theory]
    [InlineData("""{"variables":{"age":65}}""")]
    [InlineData("""{"variables":{"age":65},"values":{"age":"number"}}""")]
    [InlineData("""{"variables":{"age":65},"values":[42,{"name":"Age","dataType":"number"},{"variableName":"age","name":"Age"}]}""")]
    public void Read_MissingOrMalformedValues_SkipsThemInsteadOfThrowing(string snapshot)
    {
        // Act
        var read = PersonalizationReader.Read(snapshot);

        // Assert
        read.Should().NotBeNull();
        read!.Values.Should().BeEmpty();
        read.Variables.GetProperty("age").GetInt32().Should().Be(65);
    }

    [Fact]
    public void Read_Serialized_KeepsValuesOffTheWire()
    {
        // Arrange
        const string snapshot = """
            {"identifier":"CG-1","variables":{"age":65},"values":[{"variableName":"age","name":"Age","dataType":"number","value":65}]}
            """;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(PersonalizationReader.Read(snapshot));

        // Assert
        json.Should().NotContain("Values").And.NotContain("dataType");
    }
}
