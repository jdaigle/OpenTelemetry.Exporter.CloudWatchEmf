namespace OpenTelemetry.Exporter.CloudWatchEmf;

/// <summary>
/// Options for the <see cref="CloudWatchEmfMetricExporter" />.
/// </summary>
public class CloudWatchEmfMetricExporterOptions
{
    /// <summary>
    /// Gets or sets the metric export interval in milliseconds.
    /// Defaults to 60 seconds (60000);
    /// </summary>
    public int ExportIntervalMilliseconds { get; set; } = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;

    /// <summary>
    /// Gets or sets the metric export timeout in milliseconds.
    /// Defaults to 30 seconds (30000);
    /// </summary>
    public int ExportTimeoutMilliseconds { get; set; } = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

    /// <summary>
    /// An array of Resource attribute keys to include as dimensions.
    /// Defaults to an empty array.
    /// </summary>
    public string[] ResourceAttributesToIncludeAsDimensions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Options for the <see cref="CloudWatchPusher"/>.
    /// </summary>
    public CloudWatchPusherOptions CloudWatchPusherOptions { get; set; } = new CloudWatchPusherOptions();
}
