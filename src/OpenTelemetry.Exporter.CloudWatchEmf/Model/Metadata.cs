using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Model;

/// <summary>
/// Represents the CloudWatch Metrics Metadata appended to the CloudWatch log
/// and used by CloudWatch to parse metrics out of the log's properties.
/// </summary>
public class Metadata
{
    public Metadata()
    {
        CloudWatchMetrics = new List<MetricDirective>() { new MetricDirective() };
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// A number representing the time stamp used for metrics extracted from the event.
    /// Values MUST be expressed as the number of milliseconds after Jan 1, 1970 00:00:00 UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// An array of MetricDirective object used to instruct CloudWatch to extract metrics from the root node of the LogEvent.
    /// NOTE: serialization emits an Array, but only a single MetricDirective is supported by this library.
    /// </summary>
    public List<MetricDirective> CloudWatchMetrics { get; set; }

    public MetricDirective MetricDirective => CloudWatchMetrics.First();

    public void WriteJson(JsonTextWriter writer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("Timestamp");
        writer.WriteValue(Timestamp.ToUnixTimeMilliseconds());

        writer.WritePropertyName("CloudWatchMetrics");
        writer.WriteStartArray();
        foreach (var metricDirective in CloudWatchMetrics)
        {
            metricDirective.WriteJson(writer);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
