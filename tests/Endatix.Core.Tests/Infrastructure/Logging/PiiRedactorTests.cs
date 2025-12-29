using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Core.Tests.Infrastructure.Logging;

public class PiiRedactorTests
{
    #region Redact Tests

    [Fact]
    public void Redact_WithNullValue_ReturnsEmptyString()
    {
        // Act
        var result = PiiRedactor.Redact(null, SensitivityType.Email);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SensitivityType.Email)]
    [InlineData(SensitivityType.Secret)]
    [InlineData(SensitivityType.PhoneNumber)]
    [InlineData(SensitivityType.Name)]
    [InlineData(SensitivityType.Generic)]
    public void Redact_WithNullValue_ReturnsEmptyStringForAllTypes(SensitivityType type)
    {
        // Act
        var result = PiiRedactor.Redact(null, type);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Redact_WithGenericType_ReturnsMaskedString()
    {
        // Arrange
        var input = "sensitive-data";

        // Act
        var result = PiiRedactor.Redact(input, SensitivityType.Generic);

        // Assert
        result.Should().Be(new string('*', input.Length));
        result.Should().HaveLength(input.Length);
    }

    #endregion

    #region RedactEmail Tests

    [Theory]
    [InlineData("john.doe@example.com", "j*****e@example.com")]
    [InlineData("test@domain.com", "t*****t@domain.com")]
    public void RedactEmail_WithValidEmail_RedactsUsernameCorrectly(string email, string expected)
    {
        // Act
        var result = PiiRedactor.Redact(email, SensitivityType.Email);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RedactEmail_WithShortUsername_ReturnsMaskedEmail()
    {
        // Arrange
        var email = "ab@example.com";

        // Act
        var result = PiiRedactor.Redact(email, SensitivityType.Email);

        // Assert
        result.Should().Be("*@example.com");
    }

    [Fact]
    public void RedactEmail_WithSingleCharacterUsername_ReturnsMaskedEmail()
    {
        // Arrange
        var email = "a@example.com";

        // Act
        var result = PiiRedactor.Redact(email, SensitivityType.Email);

        // Assert
        result.Should().Be("*@example.com");
    }

    [Fact]
    public void RedactEmail_WithInvalidEmailFormat_ReturnsFullyMasked()
    {
        // Arrange
        var invalidEmail = "not-an-email";

        // Act
        var result = PiiRedactor.Redact(invalidEmail, SensitivityType.Email);

        // Assert
        result.Should().Be("*****@*****");
    }

    [Fact]
    public void RedactEmail_WithMultipleAtSymbols_ReturnsFullyMasked()
    {
        // Arrange
        var invalidEmail = "test@test@example.com";

        // Act
        var result = PiiRedactor.Redact(invalidEmail, SensitivityType.Email);

        // Assert
        result.Should().Be("*****@*****");
    }

    [Fact]
    public void RedactEmail_WithEmptyString_ReturnsFullyMasked()
    {
        // Arrange
        var emptyEmail = string.Empty;

        // Act
        var result = PiiRedactor.Redact(emptyEmail, SensitivityType.Email);

        // Assert
        result.Should().Be("*****@*****");
    }

    #endregion

    #region RedactSecret Tests

    [Fact]
    public void RedactSecret_ReturnsRandomLengthMask()
    {
        // Act
        var result = PiiRedactor.Redact("any-secret", SensitivityType.Secret);

        // Assert
        result.Should().MatchRegex(@"^\*+$");
        result.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
    }

    [Fact]
    public void RedactSecret_WithDifferentInputs_ReturnsDifferentLengths()
    {
        // Arrange
        var results = new HashSet<int>();

        // Act - Run multiple times to get different random lengths
        for (var i = 0; i < 100; i++)
        {
            var result = PiiRedactor.Redact($"secret-{i}", SensitivityType.Secret);
            results.Add(result.Length);
        }

        // Assert - Should have multiple different lengths (not all the same)
        results.Count.Should().BeGreaterThan(1);
        results.Min().Should().BeGreaterThanOrEqualTo(PiiRedactor.SECRET_LENGTH_MIN_LENGTH);
        results.Max().Should().BeLessThan(PiiRedactor.SECRET_LENGTH_MAX_LENGTH);
    }

    [Fact]
    public void RedactSecret_IgnoresInputValue()
    {
        // Arrange
        var shortSecret = "abc";
        var longSecret = "this-is-a-very-long-secret-value-that-should-be-ignored";

        // Act
        var shortResult = PiiRedactor.Redact(shortSecret, SensitivityType.Secret);
        var longResult = PiiRedactor.Redact(longSecret, SensitivityType.Secret);

        // Assert - Both should be random lengths, not based on input
        shortResult.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
        longResult.Length.Should().BeInRange(
            PiiRedactor.SECRET_LENGTH_MIN_LENGTH,
            PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
    }

    #endregion

    #region RedactPhoneNumber Tests

    [Theory]
    [InlineData("+1234567890", "+123***7890")]
    [InlineData("+441234567890", "+441*****7890")]
    [InlineData("+11234567890", "+112****7890")]
    [InlineData("+1 (345) 678-9012", "+1 (***) ***-9012")]
    [InlineData("+62 234567890", "+62 *****7890")]
    [InlineData("+359 889234567", "+359 *****4567")]
    public void RedactPhoneNumber_WithCountryCode_KeepsCountryCodeAndLastFourDigits(string phoneNumber, string expected)
    {
        // Act
        var result = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RedactPhoneNumber_WithoutCountryCode_KeepsLastFourDigits()
    {
        // Arrange
        var phoneNumber = "1234567890";

        // Act
        var result = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Assert
        result.Should().Be("******7890");
    }

    [Fact]
    public void RedactPhoneNumber_WithLessThanFourDigits_ReturnsFullyMasked()
    {
        // Arrange
        var phoneNumber = "123";

        // Act
        var result = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Assert
        result.Should().Be("***");
        result.Should().HaveLength(phoneNumber.Length);
    }

    [Fact]
    public void RedactPhoneNumber_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var phoneNumber = string.Empty;

        // Act
        var result = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void RedactPhoneNumber_WithNonDigitCharacters_PreservesNonDigits()
    {
        // Arrange
        var phoneNumber = "+1 (234) 567-890";

        // Act
        var result = PiiRedactor.Redact(phoneNumber, SensitivityType.PhoneNumber);

        // Assert
        result.Should().Contain("(");
        result.Should().Contain(")");
        result.Should().Contain("-");
        result.Should().Contain(" ");
        result.Should().EndWith("890");
    }

    #endregion

    #region RedactName Tests

    [Theory]
    [InlineData("John Doe", "J*** D**")]
    [InlineData("Mary Jane Watson", "M*** J*** W*****")]
    [InlineData("Alice", "A****")]
    public void RedactName_WithValidName_RedactsCorrectly(string name, string expected)
    {
        // Act
        var result = PiiRedactor.Redact(name, SensitivityType.Name);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RedactName_WithSingleCharacter_ReturnsAsterisk()
    {
        // Arrange
        var name = "A";

        // Act
        var result = PiiRedactor.Redact(name, SensitivityType.Name);

        // Assert
        result.Should().Be("*");
    }

    [Fact]
    public void RedactName_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var name = string.Empty;

        // Act
        var result = PiiRedactor.Redact(name, SensitivityType.Name);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void RedactName_WithMultipleSpaces_HandlesCorrectly()
    {
        // Arrange
        var name = "John    Doe";

        // Act
        var result = PiiRedactor.Redact(name, SensitivityType.Name);

        // Assert
        result.Should().Be("J*** D**");
    }

    [Fact]
    public void RedactName_WithLeadingAndTrailingSpaces_TrimsCorrectly()
    {
        // Arrange
        var name = "  John Doe  ";

        // Act
        var result = PiiRedactor.Redact(name, SensitivityType.Name);

        // Assert
        result.Should().Be("J*** D**");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Redact_WithNonStringObject_CallsToString()
    {
        // Arrange
        var number = 12345;

        // Act
        var result = PiiRedactor.Redact(number, SensitivityType.Generic);

        // Assert
        result.Should().Be(new string('*', number.ToString().Length));
    }

    [Fact]
    public void Redact_WithCustomObject_CallsToString()
    {
        // Arrange
        var customObject = new TestObject { Value = "test" };

        // Act
        var result = PiiRedactor.Redact(customObject, SensitivityType.Generic);

        // Assert
        result.Should().Be(new string('*', customObject.ToString()!.Length));
    }

    private class TestObject
    {
        public string Value { get; set; } = string.Empty;

        public override string ToString() => $"TestObject({Value})";
    }

    #endregion
}

