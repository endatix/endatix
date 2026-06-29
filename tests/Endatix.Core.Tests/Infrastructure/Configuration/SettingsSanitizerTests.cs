using Endatix.Core.Infrastructure.Configuration;

namespace Endatix.Core.Tests.Infrastructure.Configuration;

public sealed class SettingsSanitizerTests
{
    [Fact]
    public void HasSecret_ReturnsPresenceWithoutExposingValue()
    {
        SettingsSanitizer.HasSecret(null).Should().BeFalse();
        SettingsSanitizer.HasSecret("   ").Should().BeFalse();
        SettingsSanitizer.HasSecret("configured").Should().BeTrue();
    }

    [Fact]
    public void MaskSecret_ReturnsMaskedValue()
    {
        SettingsSanitizer.MaskSecret(null).Should().BeNull();
        SettingsSanitizer.MaskSecret("secret").Should().Be("******");
    }
}
