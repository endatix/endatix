using Endatix.Core.Branding;
using Endatix.Core.Infrastructure.Result;
using FluentAssertions;

namespace Endatix.Core.Tests.Branding;

public sealed class BrandOverrideTests
{
    private const long TENANT_ID = 42;

    private const string FULL = """
        {
          "version": 1,
          "assets": {
            "logo": { "light": "t/42/brand/logo-light.png", "dark": "t/42/brand/logo-dark.webp" },
            "icon": "t/42/brand/icon.PNG"
          },
          "tokens": {
            "color": {
              "primary": { "$type": "color", "$value": "#0052d1" },
              "primary-on-dark": { "$type": "color", "$value": "#7aa2ff" }
            }
          }
        }
        """;

    [Fact]
    public void Parse_EveryFieldATenantMaySet_IsValid()
    {
        // Act
        var result = BrandOverride.Parse(FULL, TENANT_ID);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(TENANT_ID);
    }

    [Fact]
    public void Parse_VersionOnly_IsValidAndShowsTheDeploymentBrand()
    {
        // Act
        var result = BrandOverride.Parse("""{"version":1}""", TENANT_ID);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CanonicalJson.Should().Be("""{"version":1}""");
    }

    [Fact]
    public void CanonicalJson_SameValueWrittenTwoWays_IsTheSameText()
    {
        // Arrange
        const string reordered = """
            {"tokens":{"color":{"primary":{"$value":"#0052d1","$type":"color"}}},   "version":1}
            """;
        const string ordered = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";

        // Act
        var first = BrandOverride.Parse(reordered, TENANT_ID).Value;
        var second = BrandOverride.Parse(ordered, TENANT_ID).Value;

        // Assert
        first.CanonicalJson.Should().Be(second.CanonicalJson);
        first.CanonicalJson.Should().Be("""{"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}},"version":1}""");
    }

    [Fact]
    public void ContentHash_KeysReordered_IsTheSame()
    {
        // Arrange
        const string reordered = """
            {"tokens": {"color": {"primary-on-dark": {"$type": "color", "$value": "#7aa2ff"}, "primary": {"$type": "color", "$value": "#0052d1"}}}, "assets": {"icon": "t/42/brand/icon.PNG", "logo": {"dark": "t/42/brand/logo-dark.webp", "light": "t/42/brand/logo-light.png"}}, "version": 1}
            """;

        // Act
        var value = BrandOverride.Parse(FULL, TENANT_ID).Value;
        var same = BrandOverride.Parse(reordered, TENANT_ID).Value;
        var other = BrandOverride.Parse("""{"version":1}""", TENANT_ID).Value;

        // Assert
        value.ContentHash.Should().Be(same.ContentHash).And.HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
        value.ContentHash.Should().NotBe(other.ContentHash);
    }

    [Theory]
    [InlineData("""{"version":1,"name":"Other"}""", "/name")]
    [InlineData("""{"version":1,"links":{"website":"https://example.com"}}""", "/links")]
    [InlineData("""{"version":1,"email":{"senderName":"Other"}}""", "/email")]
    [InlineData("""{"version":1,"poweredBy":false}""", "/poweredBy")]
    [InlineData("""{"version":1,"personas":{"assistant":"Other"}}""", "/personas")]
    [InlineData("""{"version":1,"assets":{"ogImage":"t/42/brand/og.png"}}""", "/assets/ogImage")]
    public void Parse_DeploymentOnlyField_IsAnUnknownField(string json, string path)
    {
        // Act
        var result = BrandOverride.Parse(json, TENANT_ID);

        // Assert
        ShouldHaveOnly(result, path, BrandOverrideProblems.UNKNOWN_FIELD);
    }

    [Theory]
    [InlineData("""{}""", "/version", BrandOverrideProblems.REQUIRED)]
    [InlineData("""{"version":2}""", "/version", BrandOverrideProblems.UNSUPPORTED_VERSION)]
    [InlineData("""[]""", "", BrandOverrideProblems.INVALID_TYPE)]
    [InlineData("""{"version":1""", "", BrandOverrideProblems.INVALID_JSON)]
    [InlineData("""{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#ffff00"},"primary":{"$type":"color","$value":"#0052d1"}}}}""", "", BrandOverrideProblems.INVALID_JSON)]
    [InlineData("""{"version":1,"assets":{"logo":{"dark":"t/42/brand/d.png"}}}""", "/assets/logo/light", BrandOverrideProblems.REQUIRED)]
    [InlineData("""{"version":1,"tokens":{"color":{}}}""", "/tokens/color/primary", BrandOverrideProblems.REQUIRED)]
    [InlineData("""{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"red"}}}}""", "/tokens/color/primary/$value", BrandOverrideProblems.INVALID_COLOR)]
    [InlineData("""{"version":1,"tokens":{"color":{"primary":{"$type":"dimension","$value":"#0052d1"}}}}""", "/tokens/color/primary/$type", BrandOverrideProblems.INVALID_TYPE)]
    [InlineData("""{"version":1,"assets":{"icon":7}}""", "/assets/icon", BrandOverrideProblems.INVALID_TYPE)]
    public void Parse_MalformedOverride_NamesTheField(string json, string path, string code)
    {
        // Act
        var result = BrandOverride.Parse(json, TENANT_ID);

        // Assert
        ShouldHaveOnly(result, path, code);
    }

    [Theory]
    [InlineData("t/43/brand/logo.png")]
    [InlineData("t/4/brand/logo.png")]
    [InlineData("t/42/other/logo.png")]
    [InlineData("t/42/brand/../../43/brand/logo.png")]
    [InlineData("t/42/brand/.hidden.png")]
    [InlineData("/t/42/brand/logo.png")]
    [InlineData("https://cdn.example.com/t/42/brand/logo.png")]
    [InlineData("t/42/brand/")]
    public void Parse_AssetKeyOutsideTheTenantsBrandFolder_IsRejected(string key)
    {
        // Act
        var result = BrandOverride.Parse($$$"""{"version":1,"assets":{"icon":"{{{key}}}"}}""", TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "/assets/icon", BrandOverrideProblems.INVALID_ASSET_KEY);
    }

    [Fact]
    public void Parse_AssetKeyTooLong_IsRejected()
    {
        // Arrange
        var key = "t/42/brand/" + new string('a', 250) + ".png";

        // Act
        var result = BrandOverride.Parse($$$"""{"version":1,"assets":{"icon":"{{{key}}}"}}""", TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "/assets/icon", BrandOverrideProblems.INVALID_ASSET_KEY);
    }

    [Theory]
    [InlineData("t/42/brand/logo.svg")]
    [InlineData("t/42/brand/logo.jpg")]
    [InlineData("t/42/brand/logo")]
    public void Parse_AssetNotPngOrWebp_IsRejected(string key)
    {
        // Act
        var result = BrandOverride.Parse($$$$"""{"version":1,"assets":{"logo":{"light":"{{{{key}}}}"}}}""", TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "/assets/logo/light", BrandOverrideProblems.UNSUPPORTED_ASSET_TYPE);
    }

    [Fact]
    public void Parse_LowContrastAccent_IsRejectedWithAProblemEntry()
    {
        // Act
        var result = BrandOverride.Parse(
            """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#ffff00"}}}}""",
            TENANT_ID);

        // Assert
        var problem = ShouldHaveOnly(result, "/tokens/color/primary/$value", BrandOverrideProblems.LOW_CONTRAST);
        problem.ErrorMessage.Should().Be("The accent's contrast on the light page background is 1.07:1; it must be at least 3:1.");
    }

    [Fact]
    public void Parse_LowContrastDarkAccent_NamesTheDarkField()
    {
        // Act
        var result = BrandOverride.Parse(
            """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"},"primary-on-dark":{"$type":"color","$value":"#001a40"}}}}""",
            TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "/tokens/color/primary-on-dark/$value", BrandOverrideProblems.LOW_CONTRAST);
    }

    [Fact]
    public void Parse_SeveralProblems_ListsEach()
    {
        // Act
        var result = BrandOverride.Parse(
            """{"version":1,"name":"Other","assets":{"icon":"t/7/brand/i.png"},"tokens":{"color":{"primary":{"$type":"color","$value":"nope"}}}}""",
            TENANT_ID);

        // Assert
        result.ValidationErrors.Select(error => (error.Identifier, error.ErrorCode)).Should().BeEquivalentTo(new[]
        {
            ("/name", BrandOverrideProblems.UNKNOWN_FIELD),
            ("/assets/icon", BrandOverrideProblems.INVALID_ASSET_KEY),
            ("/tokens/color/primary/$value", BrandOverrideProblems.INVALID_COLOR),
        });
    }

    [Fact]
    public void Parse_TextOverTheLimit_IsTooLargeAndNotRead()
    {
        // Arrange
        var json = "{\"version\":1" + new string(' ', BrandOverride.MAX_JSON_LENGTH) + "}";

        // Act
        var result = BrandOverride.Parse(json, TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "", BrandOverrideProblems.TOO_LARGE);
    }

    [Fact]
    public void Parse_TextAtTheLimit_IsRead()
    {
        // Arrange
        const string start = "{\"version\":1";
        var json = start + new string(' ', BrandOverride.MAX_JSON_LENGTH - start.Length - 1) + "}";

        // Act
        var result = BrandOverride.Parse(json, TENANT_ID);

        // Assert
        json.Length.Should().Be(BrandOverride.MAX_JSON_LENGTH);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Parse_ColourLongerThanTheLimit_IsAnInvalidColour()
    {
        // Arrange: a colour the patterns would accept, padded past the limit
        var value = "rgb(0 82 209" + new string(' ', BrandOverrideReader.COLOR_MAX_LENGTH) + ")";

        // Act
        var result = BrandOverride.Parse(
            "{\"version\":1,\"tokens\":{\"color\":{\"primary\":{\"$type\":\"color\",\"$value\":\"" + value + "\"}}}}",
            TENANT_ID);

        // Assert
        BrandColor.Parse(value).Should().NotBeNull();
        ShouldHaveOnly(result, "/tokens/color/primary/$value", BrandOverrideProblems.INVALID_COLOR);
    }

    [Fact]
    public void Parse_ManyUnknownFields_ReportsAtMostTheCap()
    {
        // Arrange
        var fields = string.Concat(Enumerable.Range(0, 30).Select(i => $",\"extra{i}\":{i}"));

        // Act
        var result = BrandOverride.Parse("{\"version\":1" + fields + ",\"assets\":{\"other\":1}}", TENANT_ID);

        // Assert
        result.ValidationErrors.Should().HaveCount(BrandOverrideReader.MAX_UNKNOWN_FIELDS)
            .And.OnlyContain(error => error.ErrorCode == BrandOverrideProblems.UNKNOWN_FIELD);
    }

    [Theory]
    [InlineData("t/42/brand/logo.png\\n")]
    [InlineData("t/42/brand/lo\\tgo.png")]
    public void Parse_AssetKeyWithAControlCharacter_IsRejected(string escapedKey)
    {
        // Act: the key arrives JSON-escaped, as a client sends it
        var result = BrandOverride.Parse(
            "{\"version\":1,\"assets\":{\"icon\":\"" + escapedKey + "\"}}",
            TENANT_ID);

        // Assert
        ShouldHaveOnly(result, "/assets/icon", BrandOverrideProblems.INVALID_ASSET_KEY);
    }

    private static ValidationError ShouldHaveOnly(Result<BrandOverride> result, string path, string code)
    {
        result.Status.Should().Be(ResultStatus.Invalid);
        var problem = result.ValidationErrors.Should().ContainSingle().Subject;
        problem.Identifier.Should().Be(path);
        problem.ErrorCode.Should().Be(code);
        problem.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        return problem;
    }
}
