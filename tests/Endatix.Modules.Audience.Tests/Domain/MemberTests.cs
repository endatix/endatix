using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class MemberTests
{
    private static readonly IValueNormalizer EmailNormalizer = new UpperInvariantNormalizer();

    [Fact]
    public void Constructor_EmailKind_UsesValueNormalizer()
    {
        // Arrange
        const string identifier = "  Ada@Example.COM ";

        // Act
        Member member = new(new MemberCreateArgs(
            1, identifier, AudienceIdentifierKindCodes.Email, EmailNormalizer));

        // Assert
        member.Identifier.Should().Be("ADA@EXAMPLE.COM");
    }

    [Fact]
    public void Constructor_ExternalIdKind_TrimsAndKeepsCase()
    {
        // Arrange
        const string identifier = " CRM-00Qx7 ";

        // Act
        Member member = new(new MemberCreateArgs(
            1, identifier, AudienceIdentifierKindCodes.ExternalId, EmailNormalizer));

        // Assert
        member.Identifier.Should().Be("CRM-00Qx7");
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("phone")]
    public void Constructor_UnknownKind_Throws(string identifierKind)
    {
        Action act = () => new Member(new MemberCreateArgs(
            1, "ada@example.com", identifierKind, EmailNormalizer));

        act.Should().Throw<ArgumentException>();
    }

    private sealed class UpperInvariantNormalizer : IValueNormalizer
    {
        public string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
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
