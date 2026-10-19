using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class MembershipTests
{
    [Fact]
    public void Constructor_WithoutIsTest_IsARealPerson()
    {
        // Act
        Membership membership = new(new MembershipCreateArgs(1, 2, 3));

        // Assert
        membership.IsTest.Should().BeFalse();
    }

    [Fact]
    public void Constructor_IsTest_IsATestPerson()
    {
        // Act
        Membership membership = new(new MembershipCreateArgs(1, 2, 3, IsTest: true));

        // Assert
        membership.IsTest.Should().BeTrue();
    }
}
