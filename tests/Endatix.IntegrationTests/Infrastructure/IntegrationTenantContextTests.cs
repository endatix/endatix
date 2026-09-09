namespace Endatix.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class IntegrationTenantContextTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveTenantId_ThrowsArgumentException(long tenantId)
    {
        Action act = () => new IntegrationTenantContext(tenantId);

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void Bypass_IsTenantZero_SoTheQueryFilterIsOff()
    {
        IntegrationTenantContext.Bypass.TenantId.Should().Be(0);
    }
}
