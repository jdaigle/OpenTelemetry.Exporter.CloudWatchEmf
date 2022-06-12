namespace OpenTelemetry.Exporter.CloudWatchEmf;

/// <summary>
/// Options for the <see cref="CloudWatchEmfMetricExporter" />.
/// </summary>
public class CloudWatchEmfMetricExporterOptions
{
    /// <summary>
    /// Gets or sets the metric export interval in milliseconds.
    /// </summary>
    public int ExportIntervalMilliseconds { get; set; } = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;

    /// <summary>
    /// Gets or sets the metric export timeout in milliseconds.
    /// </summary>
    public int ExportTimeoutMilliseconds { get; set; } = Timeout.Infinite;

    /// <summary>
    /// Options for the <see cref="CloudWatchPusher"/>.
    /// </summary>
    public CloudWatchPusherOptions CloudWatchPusherOptions { get; set; } = new CloudWatchPusherOptions();
}
