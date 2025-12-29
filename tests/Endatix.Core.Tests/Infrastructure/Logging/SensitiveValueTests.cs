using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Core.Tests.Infrastructure.Logging;

public class SensitiveValueTests
{
    #region Email Tests

    [Fact]
    public void Email_WithValidEmail_CreatesSensitiveValueWithEmailType()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var sensitiveValue = SensitiveValue.Email(email);

        // Assert
        sensitiveValue.Value.Should().Be(email);
        sensitiveValue.Type.Should().Be(SensitivityType.Email);
    }

    [Fact]
    public void Email_ToString_ReturnsRedactedEmail()
    {
        // Arrange
        var email = "john.doe@example.com";
        var sensitiveValue = SensitiveValue.Email(email);
        var redactedEmail = PiiRedactor.Redact(email, SensitivityType.Email);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        result.Should().Be(redactedEmail);
    }

    #endregion

    #region Secret Tests

    [Fact]
    public void Secret_WithValidSecret_CreatesSensitiveValueWithSecretType()
    {
        // Arrange
        var secret = "my-secret-password";

        // Act
        var sensitiveValue = SensitiveValue.Secret(secret);

        // Assert
        sensitiveValue.Value.Should().Be(secret);
        sensitiveValue.Type.Should().Be(SensitivityType.Secret);
    }

    [Fact]
    public void Secret_ToString_ReturnsRandomLengthMask()
    {
        // Arrange
        var secret = "my-secret-password";
        var sensitiveValue = SensitiveValue.Secret(secret);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        result.Should().MatchRegex(@"^\*+$");
        result.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
    }

    [Fact]
    public void Secret_ToString_IgnoresInputLength()
    {
        // Arrange
        var shortSecret = "abc";
        var longSecret = "this-is-a-very-long-secret-value";
        var shortSensitiveValue = SensitiveValue.Secret(shortSecret);
        var longSensitiveValue = SensitiveValue.Secret(longSecret);

        // Act
        var shortResult = shortSensitiveValue.ToString();
        var longResult = longSensitiveValue.ToString();

        // Assert
        shortResult.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
        longResult.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
    }

    #endregion

    #region PhoneNumber Tests

    [Fact]
    public void PhoneNumber_WithValidPhoneNumber_CreatesSensitiveValueWithPhoneNumberType()
    {
        // Arrange
        var phoneNumber = "+1234567890";

        // Act
        var sensitiveValue = SensitiveValue.PhoneNumber(phoneNumber);

        // Assert
        sensitiveValue.Value.Should().Be(phoneNumber);
        sensitiveValue.Type.Should().Be(SensitivityType.PhoneNumber);
    }

    [Fact]
    public void PhoneNumber_ToString_ReturnsRedactedPhoneNumber()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        var sensitiveValue = SensitiveValue.PhoneNumber(phoneNumber);
        var redactedPhoneNumber = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        result.Should().Be(redactedPhoneNumber);
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_WithValidName_CreatesSensitiveValueWithNameType()
    {
        // Arrange
        var name = "John Doe";

        // Act
        var sensitiveValue = SensitiveValue.Name(name);

        // Assert
        sensitiveValue.Value.Should().Be(name);
        sensitiveValue.Type.Should().Be(SensitivityType.Name);
    }

    [Fact]
    public void Name_ToString_ReturnsRedactedName()
    {
        // Arrange
        var name = "John Doe";
        var sensitiveValue = SensitiveValue.Name(name);
        var redactedName = PiiRedactor.Redact(name, SensitivityType.Name);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        result.Should().Be(redactedName);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValueAndType_CreatesSensitiveValue()
    {
        // Arrange
        var value = "test-value";
        var type = SensitivityType.Generic;

        // Act
        var sensitiveValue = new SensitiveValue(value, type);

        // Assert
        sensitiveValue.Value.Should().Be(value);
        sensitiveValue.Type.Should().Be(type);
    }

    [Fact]
    public void Constructor_WithOnlyValue_UsesGenericType()
    {
        // Arrange
        var value = "test-value";

        // Act
        var sensitiveValue = new SensitiveValue(value);

        // Assert
        sensitiveValue.Value.Should().Be(value);
        sensitiveValue.Type.Should().Be(SensitivityType.Generic);
    }

    [Fact]
    public void Constructor_WithNullValue_HandlesNull()
    {
        // Act
        var sensitiveValue = new SensitiveValue(null);

        // Assert
        sensitiveValue.Value.Should().BeNull();
        sensitiveValue.Type.Should().Be(SensitivityType.Generic);
    }

    [Fact]
    public void Constructor_WithNullValue_ToStringReturnsEmpty()
    {
        // Arrange
        var sensitiveValue = new SensitiveValue(null);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void SensitiveValue_WithSameValueAndType_AreEqual()
    {
        // Arrange
        var value = "test-value";
        var type = SensitivityType.Email;
        var sensitiveValue1 = new SensitiveValue(value, type);
        var sensitiveValue2 = new SensitiveValue(value, type);

        // Act & Assert
        sensitiveValue1.Should().Be(sensitiveValue2);
        (sensitiveValue1 == sensitiveValue2).Should().BeTrue();
    }

    [Fact]
    public void SensitiveValue_WithDifferentValue_AreNotEqual()
    {
        // Arrange
        var type = SensitivityType.Email;
        var sensitiveValue1 = new SensitiveValue("value1", type);
        var sensitiveValue2 = new SensitiveValue("value2", type);

        // Act & Assert
        sensitiveValue1.Should().NotBe(sensitiveValue2);
        (sensitiveValue1 != sensitiveValue2).Should().BeTrue();
    }

    [Fact]
    public void SensitiveValue_WithDifferentType_AreNotEqual()
    {
        // Arrange
        var value = "test-value";
        var sensitiveValue1 = new SensitiveValue(value, SensitivityType.Email);
        var sensitiveValue2 = new SensitiveValue(value, SensitivityType.Secret);

        // Act & Assert
        sensitiveValue1.Should().NotBe(sensitiveValue2);
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData(SensitivityType.Email, "john.doe@example.com", "j*****e@example.com")]
    [InlineData(SensitivityType.Name, "John Doe", "J*** D**")]
    [InlineData(SensitivityType.PhoneNumber, "+1234567890", "+123***7890")]
    [InlineData(SensitivityType.Generic, "test-value", "**********")]
    public void ToString_WithDifferentTypes_ReturnsAppropriateRedaction(
        SensitivityType type,
        string value,
        string expected)
    {
        // Arrange
        var sensitiveValue = new SensitiveValue(value, type);

        // Act
        var result = sensitiveValue.ToString();

        // Assert
        if (type == SensitivityType.Secret)
        {
            // Secret has random length, so just check it's all asterisks
            result.Should().MatchRegex(@"^\*+$");
            result.Length.Should().BeInRange(
                PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
                PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
        }
        else if (type == SensitivityType.Generic)
        {
            // Generic masks to the same length
            result.Should().HaveLength(value.Length);
            result.Should().MatchRegex(@"^\*+$");
        }
        else
        {
            result.Should().Be(expected);
        }
    }

    #endregion
}

