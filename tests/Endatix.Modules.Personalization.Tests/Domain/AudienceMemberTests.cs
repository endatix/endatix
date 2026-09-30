using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;

namespace Endatix.Modules.Personalization.Tests.Domain;

public class AudienceMemberTests
{
    [Fact]
    public void Constructor_NormalizesIdentifier()
    {
        AudienceMember member = new(tenantId: 1, identifier: "  Ada@Example.COM ");

        member.Identifier.Should().Be("ada@example.com");
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
