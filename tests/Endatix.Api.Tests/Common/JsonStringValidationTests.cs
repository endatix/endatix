using Endatix.Api.Common;

namespace Endatix.Api.Tests.Common;

public class JsonStringValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_NullOrEmpty_ReturnsTrue(string? json)
    {
        var result = JsonStringValidation.IsValid(json);

        Assert.True(result);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"string\"")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("{\"key\":\"value\"}")]
    [InlineData("[{\"key\":\"value\"}]")]
    [InlineData("{\"name\":\"test\",\"value\":123}")]
    [InlineData("{\"nested\":{\"key\":\"value\"}}")]
    [InlineData("{\"array\":[1,2,3]}")]
    public void IsValid_ValidJson_ReturnsTrue(string json)
    {
        var result = JsonStringValidation.IsValid(json);

        Assert.True(result);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("[")]
    [InlineData("invalid")]
    [InlineData("{key:value}")]
    [InlineData("{key: value}")]
    public void IsValid_InvalidJson_ReturnsFalse(string invalidJson)
    {
        var result = JsonStringValidation.IsValid(invalidJson);

        Assert.False(result);
    }
}