using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class MemberTests
{
    private static readonly IValueNormalizer EmailNormalizer = new UpperInvariantNormalizer();

    [Fact]
    public void Constructor_EmailKind_KeepsIdentifierAndNormalizesMatchKey()
    {
        // Arrange
        const string identifier = "  Ada@Example.COM ";

        // Act
        Member member = new(new MemberCreateArgs(
            1, identifier, AudienceIdentifierKindCodes.Email, EmailNormalizer));

        // Assert
        member.Identifier.Should().Be("Ada@Example.COM");
        member.NormalizedIdentifier.Should().Be("ADA@EXAMPLE.COM");
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
        member.NormalizedIdentifier.Should().Be("CRM-00Qx7");
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("phone")]
    public void Constructor_UnknownKind_ThrowsArgumentException(string identifierKind)
    {
        // Arrange
        MemberCreateArgs args = new(1, "ada@example.com", identifierKind, EmailNormalizer);

        // Act
        Action act = () => new Member(args);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmailKindWithInvalidAddress_ThrowsDomainValidationException()
    {
        // Arrange
        MemberCreateArgs args = new(1, "John Smith", AudienceIdentifierKindCodes.Email, EmailNormalizer);

        // Act
        Action act = () => new Member(args);

        // Assert
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("John Smith")]
    [InlineData("Ada <ada@example.com>")]
    [InlineData("ada@example.com, bob@example.com")]
    public void IdentifierError_EmailKindWithInvalidAddress_ReturnsMessage(string identifier)
    {
        // Act
        string? error = Member.IdentifierError(identifier, AudienceIdentifierKindCodes.Email);

        // Assert
        error.Should().Contain("not a valid email address");
    }

    [Fact]
    public void IdentifierError_ExternalIdKindWithAnyText_ReturnsNull()
    {
        // Act
        string? error = Member.IdentifierError("John Smith", AudienceIdentifierKindCodes.ExternalId);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void IdentifierError_Blank_ReturnsRequiredMessage()
    {
        // Act
        string? error = Member.IdentifierError("   ", AudienceIdentifierKindCodes.ExternalId);

        // Assert
        error.Should().Be("Identifier is required.");
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
    public void Constructor_NoKind_DefaultsToEmail()
    {
        // Act
        AudienceSettings settings = new(tenantId: 1);

        // Assert
        settings.IdentifierKind.Should().Be(AudienceIdentifierKindCodes.Email);
    }

    [Fact]
    public void SetIdentifierKind_UnknownKind_ThrowsArgumentException()
    {
        // Arrange
        AudienceSettings settings = new(tenantId: 1);

        // Act
        Action act = () => settings.SetIdentifierKind("phone");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
