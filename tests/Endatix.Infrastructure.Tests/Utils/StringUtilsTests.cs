using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class StringUtilsTests
{
    [Theory]
    [InlineData("HelloWorld", "hello-world")]
    [InlineData("helloWorld", "hello-world")]
    [InlineData("hello world", "hello-world")]
    [InlineData("hello_world", "hello-world")]
    [InlineData("hello-world", "hello-world")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("", "")]
    [InlineData("ABC", "abc")]
    [InlineData("Already-Kebab-Case", "already-kebab-case")]
    [InlineData("snake_case_example", "snake-case-example")]
    [InlineData("PascalCaseExample", "pascal-case-example")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("  123", "123")]
    [InlineData("!@#$%^&*()", "!@#$%^&*()")]
    [InlineData("a1b2c3", "a1b2c3")]
    public void ToKebabCase_WorksAsExpected(string input, string expected)
    {
        var result = StringUtils.ToKebabCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("form_created", "FormCreated")]
    [InlineData("submission_completed", "SubmissionCompleted")]
    [InlineData("user_registered", "UserRegistered")]
    [InlineData("simple", "Simple")]
    [InlineData("multiple_word_event_name", "MultipleWordEventName")]
    [InlineData("", "")]
    [InlineData("single", "Single")]
    [InlineData("with_numbers_123", "WithNumbers123")]
    [InlineData("UPPERCASE_INPUT", "UppercaseInput")]
    [InlineData("MiXeD_cAsE_InPuT", "MixedCaseInput")]
    public void ToPascalCase_WorksAsExpected(string input, string expected)
    {
        var result = StringUtils.ToPascalCase(input);
        Assert.Equal(expected, result);
    }
}