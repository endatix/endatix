namespace Endatix.IntegrationTests;

public sealed class TestTenantContextTests
{
    [Fact]
    public void Constructor_ZeroOrNegative_Throws()
    {
        var zero = () => new TestTenantContext(0);
        var negative = () => new TestTenantContext(-1);

        zero.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tenantId");
        negative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tenantId");
        TestTenantContext.Bypass.TenantId.Should().Be(0);
    }
}
