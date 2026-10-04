using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;

namespace Endatix.Modules.Personalization.Tests.Domain;

public class MemberTests
{
    [Fact]
    public void Constructor_EmailKind_TrimsAndLowerCasesIdentifier()
    {
        // Arrange
        const string identifier = "  Ada@Example.COM ";

        // Act
        Member member = new(tenantId: 1, identifier, AudienceIdentifierKindCodes.Email);

        // Assert
        member.Identifier.Should().Be("ada@example.com");
    }

    [Fact]
    public void Constructor_ExternalIdKind_TrimsAndKeepsCase()
    {
        // Arrange
        const string identifier = " CRM-00Qx7 ";

        // Act
        Member member = new(tenantId: 1, identifier, AudienceIdentifierKindCodes.ExternalId);

        // Assert
        member.Identifier.Should().Be("CRM-00Qx7");
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("phone")]
    public void Constructor_UnknownKind_Throws(string identifierKind)
    {
        Action act = () => new Member(tenantId: 1, "ada@example.com", identifierKind);

        act.Should().Throw<ArgumentException>();
    }
}

public class AudienceSettingsTests
{
    [Fact]
    public void Constructor_DefaultsToEmail()
    {
        AudienceSettings settings = new(tenantId: 1);

        settings.IdentifierKind.Should().Be(AudienceIdentifierKindCodes.Email);
    }

    [Fact]
    public void SetIdentifierKind_RejectsUnknown()
    {
        AudienceSettings settings = new(tenantId: 1);

        Action act = () => settings.SetIdentifierKind("phone");

        act.Should().Throw<ArgumentException>();
    }
}
