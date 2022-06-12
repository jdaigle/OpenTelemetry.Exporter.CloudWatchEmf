namespace OpenTelemetry.Exporter.CloudWatchEmf;

/// <summary>
/// Options for the <see cref="CloudWatchPusher"/>.
/// </summary>
public class CloudWatchPusherOptions
{
    /// <summary>
    /// The CloudWatch Log Group to send messages to.
    /// This will be created if it doesn't exist.
    /// </summary>
    public string LogGroup = "/metrics/default";

    /// <summary>
    /// Timeout for Send operations. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
