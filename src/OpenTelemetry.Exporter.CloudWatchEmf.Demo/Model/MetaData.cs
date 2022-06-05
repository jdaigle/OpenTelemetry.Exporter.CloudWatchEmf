using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

/// <summary>
/// Represents the CloudWatch Metrics Metadata appended to the CloudWatch log
/// and used by CloudWatch to parse metrics out of the log's properties.
/// </summary>
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class Metadata
{
    public Metadata()
    {
        CloudWatchMetrics = new List<MetricDirective>() { new MetricDirective() };
        Timestamp = DateTimeOffset.UtcNow;
    }

    /*internal bool IsEmpty()
    {
        return !MetricDirective.HasNoMetrics();
    }*/

    /// <summary>
    /// A number representing the time stamp used for metrics extracted from the event.
    /// Values MUST be expressed as the number of milliseconds after Jan 1, 1970 00:00:00 UTC.
    /// </summary>
    [JsonProperty("Timestamp")]
    [JsonConverter(typeof(UnixMillisecondDateTimeConverter))]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// An array of MetricDirective object used to instruct CloudWatch to extract metrics from the root node of the LogEvent.
    /// NOTE: serialization emits an Array, but only a single MetricDirective is supported by this library.
    /// </summary>
    [JsonProperty("CloudWatchMetrics")]
    public List<MetricDirective> CloudWatchMetrics { get; set; }

    public MetricDirective MetricDirective => CloudWatchMetrics.First();

    // TODO: needed?
    internal Metadata DeepCloneWithNewMetrics(List<MetricDefinition> metrics)
    {
        var clone = new Metadata
        {
            Timestamp = Timestamp,
            CloudWatchMetrics = new List<MetricDirective>()
        };
        foreach (var metric in CloudWatchMetrics)
        {
            clone.CloudWatchMetrics.Add(metric.DeepCloneWithNewMetrics(metrics));
        }
        return clone;
    }
}
