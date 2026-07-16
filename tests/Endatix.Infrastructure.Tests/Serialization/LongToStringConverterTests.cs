using System.Text.Json;
using Endatix.Framework.Serialization;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Serialization;

public class LongToStringConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new LongToStringConverter() },
    };

    [Fact]
    public void Read_PreservesSnowflakeIdFromStringToken()
    {
        const string snowflakeId = "1526934587983265792";

        long result = JsonSerializer.Deserialize<LongHolder>(
            $$"""{"value":"{{snowflakeId}}"}""",
            Options)!.Value;

        result.Should().Be(1526934587983265792L);
    }

    [Fact]
    public void Read_PreservesSnowflakeIdFromNullableStringToken()
    {
        const string snowflakeId = "1526934587983265792";

        long? result = JsonSerializer.Deserialize<NullableLongHolder>(
            $$"""{"value":"{{snowflakeId}}"}""",
            Options)!.Value;

        result.Should().Be(1526934587983265792L);
    }

    [Fact]
    public void Write_SerializesLongAsString()
    {
        const long snowflakeId = 1526934587983265792L;

        string json = JsonSerializer.Serialize(new LongHolder { Value = snowflakeId }, Options);

        json.Should().Be("""{"value":"1526934587983265792"}""");
    }

    [Fact]
    public void Read_ExportFormatIdRequestBody_PreservesSnowflakeId()
    {
        const string snowflakeId = "1526934587983265792";

        ExportFormatRequest? result = JsonSerializer.Deserialize<ExportFormatRequest>(
            $$"""{"exportFormatId":"{{snowflakeId}}"}""",
            Options);

        result!.ExportFormatId.Should().Be(1526934587983265792L);
    }

    private sealed class LongHolder
    {
        public long Value { get; set; }
    }

    private sealed class NullableLongHolder
    {
        public long? Value { get; set; }
    }

    private sealed class ExportFormatRequest
    {
        public long? ExportFormatId { get; set; }
    }
}
