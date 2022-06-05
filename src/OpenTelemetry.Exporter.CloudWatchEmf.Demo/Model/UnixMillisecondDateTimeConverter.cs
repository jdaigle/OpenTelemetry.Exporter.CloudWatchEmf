using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

internal class UnixMillisecondDateTimeConverter : DateTimeConverterBase
{
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) => throw new NotSupportedException();

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        long milliseconds;
        if (value is DateTime dateTime)
        {
            milliseconds = ((DateTimeOffset)(dateTime.ToUniversalTime())).ToUnixTimeMilliseconds();
        }
        else if (value is DateTimeOffset dateTimeOffset)
        {
            milliseconds = dateTimeOffset.ToUnixTimeMilliseconds();
        }
        else
        {
            throw new JsonSerializationException("Expected DateTime or DateTimeOffset object value.");
        }

        if (milliseconds < 0)
        {
            throw new JsonSerializationException("Cannot convert date value that is before Unix epoch of 00:00:00 UTC on 1 January 1970.");
        }

        writer.WriteValue(milliseconds);
    }
}
